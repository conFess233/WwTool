using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WwTool.Core.Abstractions;
using WwTool.Core.Enums;
using WwTool.Core.Exceptions;
using WwTool.Core.Extensions;
using WwTool.Core.Localization;
using WwTool.Core.Models.Domain;
using WwTool.Core.Services;
using WwTool.Core.Services.Guide;
using WwTool.UI.Feedback;
using WwTool.UI.Navigation;
using WwTool.UI.Services;
using WwTool.UI.Shell;

namespace WwTool.UI.Pages.Roles;

/// <summary>角色 / 武器网格里的一格。</summary>
public abstract partial class GuideTile : ObservableObject, IGuideCard
{
    /// <summary>详情浮层打开时，对应卡片显示选中态。</summary>
    [ObservableProperty]
    private bool _isSelected;

    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string IconPath { get; init; }
    public required int Star { get; init; }
    public required bool IsLimitedFiveStar { get; init; }
    public required DateTime? FirstAcquiredAt { get; init; }
    public required int SourceOrder { get; init; }
    public required string AcquiredText { get; init; }

    public string StarsText => new('★', Math.Clamp(Star, 1, 5));
    public bool IsFiveStar => Star >= 5;
    public string AutomationId => "guide-" + Id;
}

public sealed class RoleTile : GuideTile
{
    public required int RoleId { get; init; }
    public required int Sequence { get; init; }
    public required string SequenceText { get; init; }
    public GuideRoleHoverDetails Details { get; init; } = new([], null);
    public GuideRoleBuild Build { get; init; } = GuideRoleBuild.Empty;

    /// <summary>三至五链用浅强调色，六链用强调色实底。</summary>
    public bool IsMidSequence => Sequence is >= 3 and <= 5;
    public bool IsFullSequence => Sequence >= 6;
}

/// <summary>武器悬停提示的数据。</summary>
public sealed record WeaponHover(string Text);

public sealed class WeaponTile : GuideTile
{
    public required string OwnerText { get; init; }

    public WeaponHover Hover => new(AcquiredText);
}

/// <summary>按星级分组的一段：标题、数量与卡片。</summary>
public sealed record TileGroup(string Title, string CountText, IReadOnlyList<GuideTile> Tiles);

/// <summary>页签 / 排序用的胶囊选项。</summary>
public sealed partial class ChipOption(string key) : ObservableObject
{
    public string Key { get; } = key;
    public string AutomationId => "roles-" + Key;
    [ObservableProperty] private string _title = string.Empty;
    [ObservableProperty] private string? _count;
    [ObservableProperty] private bool _isSelected;
    [ObservableProperty] private bool _isEnabled = true;
    [ObservableProperty] private string? _hint;
}

/// <summary>
/// 角色页：当前账号在官方攻略站的已有角色与已装备武器（本地快照），按星级分组。
/// “同步”从攻略站拉取；需要登录时先尝试用当前登录会话换取攻略站令牌。
/// </summary>
public sealed partial class RolesViewModel : ObservableObject, INavigationAware, IFabProvider
{
    private const string Icons = "avares://WwTool/Assets/Images/";

    private readonly IGuideRepository _guide;
    private readonly IGuideSyncService _guideSync;
    private readonly ILoginService _login;
    private readonly IUserDataService _userData;
    private readonly GameDataService _catalog;
    private readonly IConfigService _config;
    private readonly LocalizationService _localization;
    private readonly IUIStateService _uiState;
    private readonly ErrorReporter _errors;
    private readonly ILoggerService _logger;
    private readonly AvaloniaCatalogImageService _images;
    private readonly AccountActions _actions;
    private readonly CharacterDetails _details;

    private CancellationTokenSource _cts = new();
    private int _loadVersion;
    private bool _isActive;
    private bool _needsReload = true;
    private GuideSort _sort = GuideSort.Star;
    private bool _showWeapons;
    private DateTimeOffset? _lastSynced;
    private bool _hasRoleRecords;
    private bool _hasWeaponRecords;
    private IReadOnlyList<RoleTile> _roles = [];
    private IReadOnlyList<WeaponTile> _weapons = [];

    public RolesViewModel(
        AccountContext accounts,
        IGuideRepository guide,
        IGuideSyncService guideSync,
        ILoginService login,
        IUserDataService userData,
        GameDataService catalog,
        IConfigService config,
        LocalizationService localization,
        IUIStateService uiState,
        ErrorReporter errors,
        ILoggerService logger,
        AvaloniaCatalogImageService images,
        AccountActions actions,
        CharacterDetails details,
        INavigationService navigation)
    {
        _actions = actions;
        _details = details;
        // 悬浮按钮：收起态加号，展开为「刷新数据」（主，从攻略站同步）与「返回上一页」。
        FabActions =
        [
            new FabAction("refresh", "Fab_RefreshData", Icons + "Refresh.png", SyncCommand, localization),
            new FabAction("back", "Fab_Back", "Icon.Back", new RelayCommand(() => navigation.GoBack()), localization),
        ];
        Accounts = accounts;
        _guide = guide;
        _guideSync = guideSync;
        _login = login;
        _userData = userData;
        _catalog = catalog;
        _config = config;
        _localization = localization;
        _uiState = uiState;
        _errors = errors;
        _logger = logger;
        _images = images;

        Tabs = [new ChipOption("roles") { IsSelected = true }, new ChipOption("weapons")];
        Sorts = [new ChipOption("star") { IsSelected = true }, new ChipOption("name"), new ChipOption("time")];

        accounts.PropertyChanged += OnAccountChanged;
        _catalog.Changed += (_, _) => Avalonia.Threading.Dispatcher.UIThread.Post(ScheduleReload);
        _localization.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == LocalizationService.IndexerName) ScheduleReload();
        };
        RefreshTexts();
    }

    public AccountContext Accounts { get; }
    public IReadOnlyList<FabAction> FabActions { get; }
    public ObservableCollection<ChipOption> Tabs { get; }
    public ObservableCollection<ChipOption> Sorts { get; }
    public ObservableCollection<TileGroup> Groups { get; } = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SyncCommand))]
    private bool _isSyncing;

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private bool _isEmpty = true;
    [ObservableProperty] private string _emptyText = string.Empty;
    [ObservableProperty] private string _syncInfo = string.Empty;

    public void OnNavigatedTo(object? parameter)
    {
        _isActive = true;
        if (_needsReload) _ = LoadAsync();
    }

    public void OnNavigatedFrom() => _isActive = false;

    /// <summary>点击角色卡：打开角色详情弹窗，停在「我的养成」。</summary>
    [RelayCommand]
    private async Task OpenDetail(RoleTile tile)
    {
        tile.IsSelected = true;
        try
        {
            await _details.ShowAsync(new CharacterDetailRequest(tile.RoleId, tile.Name, tile.IconPath, tile.Star)
            {
                Tab = CharacterDetailTab.Build,
                Sequence = tile.Sequence,
                SequenceText = tile.SequenceText,
                Build = CharacterBuild.From(SyncInfo, tile.Details, tile.Build),
            });
        }
        finally
        {
            tile.IsSelected = false;
        }
    }

    [RelayCommand]
    private void SelectTab(ChipOption tab)
    {
        _showWeapons = tab.Key == "weapons";
        foreach (ChipOption t in Tabs) t.IsSelected = t == tab;
        Rebuild();
    }

    [RelayCommand]
    private void SelectSort(ChipOption option)
    {
        if (!option.IsEnabled) return;
        _sort = option.Key switch { "name" => GuideSort.Name, "time" => GuideSort.AcquiredTime, _ => GuideSort.Star };
        foreach (ChipOption s in Sorts) s.IsSelected = s == option;
        Rebuild();
    }

    [RelayCommand(CanExecute = nameof(CanSync))]
    private async Task SyncAsync()
    {
        string? uid = Accounts.Current?.Uid;
        if (uid is null)
        {
            _uiState.ShowToast(_localization["Toast_Error"], _localization["Msg_NoAccountSelected"], NotificationType.Error);
            return;
        }
        IsSyncing = true;
        const string task = "guide-sync";
        _uiState.ShowToast(new NotificationRequest
        {
            Title = _localization["Guide_Sync"],
            Message = _localization["Guide_Syncing"],
            Priority = NotificationPriority.Important,
            Source = "Roles",
            TaskKey = task,
        });
        string language = _config.User.AppLanguage.GetCode();
        try
        {
            try
            {
                await _guideSync.SyncAsync(uid, language);
            }
            catch (GuideAuthenticationRequiredException)
            {
                // 攻略站令牌失效：先用本次运行中最近一次登录的会话换取新令牌；
                // 没有可用会话时请用户重新登录，登录成功后再换取并重试。
                if (!await TryCaptureSessionAsync(language))
                {
                    if (!await _actions.AddAccountAsync()) throw;
                    if (!await TryCaptureSessionAsync(language)) throw;
                }
                await _guideSync.SyncAsync(uid, language);
            }
            if (Accounts.Current?.Uid != uid) return;
            await LoadAsync();
            _uiState.ShowToast(new NotificationRequest
            {
                Title = _localization["Toast_Success"],
                Message = _localization["Guide_SyncSuccess"],
                Type = NotificationType.Success,
                Priority = NotificationPriority.Important,
                Source = "Roles",
                TaskKey = task,
                IsTaskCompleted = true,
            });
        }
        catch (Exception ex)
        {
            ErrorReport? report = _errors.Report(ex, _localization["Guide_Sync"], notifyUser: false);
            _uiState.ShowToast(new NotificationRequest
            {
                Title = report?.Title ?? _localization["Toast_Error"],
                Message = ex is GuideAuthenticationRequiredException ? _localization["Guide_LoginRequired"] : report?.Message ?? ex.Message,
                Type = report?.Type ?? NotificationType.Error,
                Priority = NotificationPriority.Important,
                Source = "Roles",
                TaskKey = task,
                IsTaskCompleted = true,
            });
        }
        finally
        {
            IsSyncing = false;
        }
    }

    private bool CanSync() => !IsSyncing;

    private async Task<bool> TryCaptureSessionAsync(string language)
    {
        LoginContext context = _login.LatestAuthenticatedContext;
        if (string.IsNullOrWhiteSpace(context.CUid) || string.IsNullOrWhiteSpace(context.AccessToken)) return false;
        try
        {
            await _guideSync.CaptureSessionAsync(context.CUid, context.CName, context.AccessToken, language);
            return true;
        }
        catch (Exception ex) when (ex is GuideAuthenticationRequiredException or GuideApiException)
        {
            _logger.Debug("当前登录会话无法换取攻略站令牌。", ex);
            return false;
        }
    }

    private void OnAccountChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(AccountContext.Current)) return;
        _roles = [];
        _weapons = [];
        _lastSynced = null;
        Rebuild();
        ScheduleReload();
    }

    // 页面可见时立即重读；否则等下次进入页面。
    private void ScheduleReload()
    {
        _needsReload = true;
        if (_isActive) _ = LoadAsync();
    }

    private async Task LoadAsync()
    {
        _needsReload = false;
        _cts.Cancel();
        _cts = new CancellationTokenSource();
        CancellationToken token = _cts.Token;
        int version = ++_loadVersion;
        string? uid = Accounts.Current?.Uid;
        if (uid is null)
        {
            _roles = [];
            _weapons = [];
            Rebuild();
            return;
        }

        IsLoading = true;
        try
        {
            GuideSnapshot snapshot = await _guide.LoadSnapshotAsync(uid, token);
            AcquisitionTimes roleTimes = await _userData.ReadAcquisitionTimesAsync(uid, GuideCardPools.Roles.Select(x => (int)x).ToArray(), token);
            AcquisitionTimes weaponTimes = await _userData.ReadAcquisitionTimesAsync(uid, GuideCardPools.Weapons.Select(x => (int)x).ToArray(), token);
            if (token.IsCancellationRequested || version != _loadVersion || Accounts.Current?.Uid != uid) return;

            string language = _config.User.AppLanguage.GetCode();
            _lastSynced = snapshot.LastSyncedAtUtc;
            _hasRoleRecords = roleTimes.HasRecords;
            _hasWeaponRecords = weaponTimes.HasRecords;
            _roles = snapshot.Roles.Select(r => ToRoleTile(r, snapshot, roleTimes, language)).ToArray();
            _weapons = snapshot.Weapons.Select(w => ToWeaponTile(w, weaponTimes, language)).ToArray();
            Rebuild();
            _ = PrepareHoverImagesAsync(token);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            // 切换账号或重新加载。
        }
        catch (Exception ex)
        {
            _errors.Report(ex, _localization["Nav_RoleData"]);
        }
        finally
        {
            if (version == _loadVersion) IsLoading = false;
        }
    }

    private RoleTile ToRoleTile(GuideRoleData role, GuideSnapshot snapshot, AcquisitionTimes times, string language)
    {
        int id = ParseId(role.RoleGbId);
        bool limited = role.Star == 5 && _catalog.GetLimitedStatus(id, role.Star, true) == true;
        DateTime? acquired = limited && times.Times.TryGetValue(id, out DateTime t) ? t : null;
        GuideRoleHoverDetails hover = GuideRoleHoverMapper.Map(role.DetailJson,
            snapshot.Weapons.FirstOrDefault(x => x.OwnerRoleGbId == role.RoleGbId),
            _catalog, language, key => _localization[key], ex => _logger.Warn("角色悬浮详情快照无法解析。", ex));
        int sequence = GuideRoleHoverMapper.ResolveSequence(role, (message, ex) => _logger.Warn(message, ex));
        GuideRoleBuild build = GuideRoleHoverMapper.MapBuild(role.DetailJson, language, ex => _logger.Warn("角色养成快照无法解析。", ex));
        return new RoleTile
        {
            RoleId = id,
            Id = role.RoleGbId,
            Name = ItemName(role.RoleGbId, language),
            IconPath = $"Local/Icons/{role.RoleGbId}.png",
            Star = role.Star,
            IsLimitedFiveStar = limited,
            FirstAcquiredAt = acquired,
            SourceOrder = role.SourceOrder,
            AcquiredText = AcquiredText(acquired),
            Sequence = sequence,
            SequenceText = SequenceText(sequence),
            Details = hover,
            Build = build,
        };
    }

    private WeaponTile ToWeaponTile(GuideWeaponData weapon, AcquisitionTimes times, string language)
    {
        int id = ParseId(weapon.WeaponGbId);
        bool limited = weapon.Star == 5 && _catalog.GetLimitedStatus(id, weapon.Star, false) == true;
        DateTime? acquired = limited && times.Times.TryGetValue(id, out DateTime t) ? t : null;
        return new WeaponTile
        {
            Id = weapon.WeaponGbId + "-" + weapon.OwnerRoleGbId,
            Name = ItemName(weapon.WeaponGbId, language),
            IconPath = $"Local/Icons/{weapon.WeaponGbId}.png",
            Star = weapon.Star,
            IsLimitedFiveStar = limited,
            FirstAcquiredAt = acquired,
            SourceOrder = weapon.SourceOrder,
            AcquiredText = AcquiredText(acquired),
            OwnerText = _localization["Guide_Owner"] + ItemName(weapon.OwnerRoleGbId, language),
        };
    }

    private void Rebuild()
    {
        RefreshTexts();
        Groups.Clear();
        IEnumerable<GuideTile> tiles = _showWeapons
            ? GuideCardSorter.Sort(_weapons, _sort, x => x.Name)
            : GuideCardSorter.Sort(_roles, _sort, x => x.Name);
        foreach (var group in tiles.GroupBy(x => Math.Clamp(x.Star, 1, 5)).OrderByDescending(g => g.Key))
        {
            GuideTile[] items = group.ToArray();
            string title = group.Key switch { 5 => "Roles_FiveStar", 4 => "Roles_FourStar", 3 => "Roles_ThreeStar", 2 => "Roles_TwoStar", _ => "Roles_OneStar" };
            string count = string.Format(_localization[_showWeapons ? "Roles_WeaponCountUnit" : "Roles_RoleCountUnit"], items.Length);
            Groups.Add(new TileGroup(_localization[title], count, items));
        }
        IsEmpty = Groups.Count == 0;
    }

    private void RefreshTexts()
    {
        Tabs[0].Title = _localization["Guide_RolesTab"];
        Tabs[0].Count = _roles.Count.ToString(CultureInfo.CurrentCulture);
        Tabs[1].Title = _localization["Guide_WeaponsTab"];
        Tabs[1].Count = _weapons.Count.ToString(CultureInfo.CurrentCulture);
        Sorts[0].Title = _localization["Guide_SortStar"];
        Sorts[1].Title = _localization["Guide_SortName"];
        Sorts[2].Title = _localization["Guide_SortTime"];

        // 按获取时间排序需要本地抽卡记录；没有记录时禁用并给出原因。
        bool hasRecords = _showWeapons ? _hasWeaponRecords : _hasRoleRecords;
        Sorts[2].IsEnabled = hasRecords;
        Sorts[2].Hint = hasRecords ? null : _localization["Guide_NoLocalGacha"];
        if (!hasRecords && _sort == GuideSort.AcquiredTime)
        {
            _sort = GuideSort.Star;
            foreach (ChipOption s in Sorts) s.IsSelected = s.Key == "star";
        }

        string synced = _lastSynced is { } at
            ? at.ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)
            : _localization["Guide_NeverSynced"];
        SyncInfo = string.Format(_localization["Roles_SyncInfo"], synced);
        EmptyText = _localization[_showWeapons ? "Guide_NoWeapons" : "Guide_NoRoles"];
    }

    // 浮层只读缓存；进入页面时预载立绘和武器图，浮层显示时不再触发下载。
    private async Task PrepareHoverImagesAsync(CancellationToken token)
    {
        string[] paths = _roles.Select(x => x.Details.Weapon?.ImagePath).OfType<string>().ToArray();
        try
        {
            await Task.Run(() => _images.PrepareHoverImagesAsync(paths, token), token);
        }
        catch (OperationCanceledException)
        {
            // 离开页面或切换账号。
        }
        catch (Exception ex)
        {
            _logger.Warn("角色浮层图片预载失败，使用已有缓存或头像。", ex);
        }
    }

    private string ItemName(string id, string language) =>
        int.TryParse(id, out int resourceId) ? _catalog.GetItemById(resourceId)?.GetName(language) ?? id : id;

    private static int ParseId(string id) => int.TryParse(id, out int value) ? value : 0;

    private string AcquiredText(DateTime? acquired) => acquired is { } at
        ? string.Format(_localization["Guide_FirstAcquired"], at.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture))
        : _localization["Guide_AcquiredUnknown"];

    private string SequenceText(int sequence) => _config.User.AppLanguage switch
    {
        LanguageType.En => "S" + sequence,
        LanguageType.Ja => sequence + "共鳴",
        _ => sequence == 0 ? "零链" : "一二三四五六"[Math.Clamp(sequence, 1, 6) - 1] + "链",
    };
}
