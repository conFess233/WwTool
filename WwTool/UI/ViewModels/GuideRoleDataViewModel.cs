using Prism.Commands;
using Prism.Mvvm;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Text.Json;
using WwTool.Common.Enums;
using WwTool.Common.Exceptions;
using WwTool.Common.Models.ApiResponse;
using WwTool.Common.Models.Domain;
using WwTool.Common.Utils;
using WwTool.Extensions;
using WwTool.Services;
using WwTool.Services.Interfaces;
using WwTool.Services.Presentation;

namespace WwTool.UI.ViewModels;

public sealed class GuideRoleDataViewModel : BindableBase, INavigationAware
{
    private readonly IUserDataService userDataService;
    private readonly IGuideRepository guideRepository;
    private readonly IGuideSyncService guideSyncService;
    private readonly ILoginService loginService;
    private readonly IConfigService configService;
    private readonly IUIStateService uiStateService;
    private readonly IDialogService dialogService;
    private readonly GameDataService gameDataService;
    private readonly ILoggerService logger;
    private readonly CatalogImageService? imageService;
    private CancellationTokenSource navigationCts = new();
    private bool isSelectingInitialAccount;
    private bool isBusy;
    private int loadVersion;
    private AccountSummary? selectedUser;
    private string roleSortKey = "star";
    private string weaponSortKey = "star";
    private DateTimeOffset? lastSyncedAtUtc;
    private bool hasRoleGachaRecords;
    private bool hasWeaponGachaRecords;

    public ObservableCollection<AccountSummary> Users { get; } = [];
    public ObservableCollection<GuideRoleCardViewModel> Roles { get; } = [];
    public ObservableCollection<GuideWeaponCardViewModel> Weapons { get; } = [];
    public ObservableCollection<GuideSortOption> RoleSortOptions { get; } = [];
    public ObservableCollection<GuideSortOption> WeaponSortOptions { get; } = [];

    public DelegateCommand SyncCommand { get; }

    public GuideRoleDataViewModel(
        IUserDataService userDataService,
        IGuideRepository guideRepository,
        IGuideSyncService guideSyncService,
        ILoginService loginService,
        IConfigService configService,
        IUIStateService uiStateService,
        IDialogService dialogService,
        GameDataService gameDataService,
        ILoggerService logger,
        CatalogImageService? imageService = null)
    {
        this.userDataService = userDataService;
        System.Windows.WeakEventManager<IUserDataService, AccountDeletedEventArgs>.AddHandler(userDataService, nameof(IUserDataService.AccountDeleted), OnAccountDeleted);
        this.guideRepository = guideRepository;
        this.guideSyncService = guideSyncService;
        this.loginService = loginService;
        this.configService = configService;
        this.uiStateService = uiStateService;
        this.dialogService = dialogService;
        this.gameDataService = gameDataService;
        this.logger = logger;
        this.imageService = imageService;
        if (imageService is not null)
            System.Windows.WeakEventManager<CatalogImageService, EventArgs>.AddHandler(imageService, nameof(CatalogImageService.RetryRequested), OnImageRetryRequested);
        SyncCommand = new DelegateCommand(Sync, () => !IsBusy && SelectedUser is not null)
            .ObservesProperty(() => IsBusy).ObservesProperty(() => SelectedUser);
        RefreshSortOptions();
        LanguageManager.Instance.PropertyChanged += OnLanguageChanged;
        gameDataService.Changed += OnCatalogChanged;
    }

    public bool IsBusy
    {
        get => isBusy;
        private set => SetProperty(ref isBusy, value);
    }

    public AccountSummary? SelectedUser
    {
        get => selectedUser;
        set
        {
            if (!SetProperty(ref selectedUser, value)) return;
            navigationCts.Cancel();
            navigationCts.Dispose();
            navigationCts = new();
            ++loadVersion;
            if (value is null || isSelectingInitialAccount)
                return;
            _ = SelectAccountAsync(value);
        }
    }

    public GuideSortOption? SelectedRoleSort
    {
        get => RoleSortOptions.FirstOrDefault(x => x.Key == roleSortKey);
        set
        {
            if (value is null || !value.IsEnabled || roleSortKey == value.Key) return;
            roleSortKey = value.Key;
            RaisePropertyChanged();
            SortRoles();
        }
    }

    public GuideSortOption? SelectedWeaponSort
    {
        get => WeaponSortOptions.FirstOrDefault(x => x.Key == weaponSortKey);
        set
        {
            if (value is null || !value.IsEnabled || weaponSortKey == value.Key) return;
            weaponSortKey = value.Key;
            RaisePropertyChanged();
            SortWeapons();
        }
    }

    public int RoleCount => Roles.Count;
    public int WeaponCount => Weapons.Count;
    public bool HasRoles => Roles.Count > 0;
    public bool HasWeapons => Weapons.Count > 0;
    public string LastSyncedText => lastSyncedAtUtc is null
        ? LanguageManager.Instance["Guide_NeverSynced"]
        : lastSyncedAtUtc.Value.ToLocalTime().ToString("yyyy-MM-dd HH:mm");

    public bool IsNavigationTarget(NavigationContext navigationContext) => true;
    public void OnNavigatedFrom(NavigationContext navigationContext) => navigationCts.Cancel();
    /// <summary>账号删除后取消读取并释放页面上的账号内容。</summary>
    private void OnAccountDeleted(object? sender, AccountDeletedEventArgs e)
    {
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            foreach (var user in Users.Where(x => x.Uid == e.Uid).ToArray()) Users.Remove(user);
            if (SelectedUser?.Uid != e.Uid) return;
            ++loadVersion;
            navigationCts.Cancel();
            navigationCts.Dispose();
            navigationCts = new CancellationTokenSource();
            SelectedUser = null;
            Roles.Clear(); Weapons.Clear(); lastSyncedAtUtc = null;
            hasRoleGachaRecords = hasWeaponGachaRecords = false;
            RaiseCounts();
        });
    }
    public async void OnNavigatedTo(NavigationContext navigationContext)
    {
        if (navigationCts.IsCancellationRequested)
        {
            navigationCts.Dispose();
            navigationCts = new CancellationTokenSource();
        }
        await LoadAccountsAsync();
    }

    private async Task LoadAccountsAsync()
    {
        try
        {
            CancellationToken token = navigationCts.Token;
            IReadOnlyList<AccountSummary> accounts = await userDataService.ListAccountsAsync(token);
            if (token.IsCancellationRequested) return;
            isSelectingInitialAccount = true;
            Users.Clear();
            foreach (AccountSummary account in accounts) Users.Add(account);
            SelectedUser = Users.FirstOrDefault(x => x.Uid == configService.User.LastUserId) ?? Users.FirstOrDefault();
            isSelectingInitialAccount = false;
            if (SelectedUser is not null) await LoadSnapshotAsync(SelectedUser.Uid);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { logger.Warn("加载 Guide 本地账号失败。", ex); }
        finally { isSelectingInitialAccount = false; }
    }

    private async Task SelectAccountAsync(AccountSummary account)
    {
        try
        {
            CancellationToken token = navigationCts.Token;
            configService.User.LastUserId = account.Uid;
            await configService.SaveAllAsync();
            if (token.IsCancellationRequested || SelectedUser?.Uid != account.Uid) return;
            loginService.SwitchUserContext(account.Uid);
            await LoadSnapshotAsync(account.Uid);
        }
        catch (OperationCanceledException) { /* 账号切换或离开页面。 */ }
        catch (Exception ex) { logger.Warn("加载 Guide 账号失败。", ex); }
    }

    private async Task LoadSnapshotAsync(string uid)
    {
        int version = ++loadVersion;
        CancellationToken token = navigationCts.Token;
        GuideSnapshot snapshot = await guideRepository.LoadSnapshotAsync(uid, token);
        (Dictionary<int, DateTime> roleTimes, bool roleRecords) = await LoadAcquisitionTimesAsync(uid, GuideCardPools.Roles, token);
        (Dictionary<int, DateTime> weaponTimes, bool weaponRecords) = await LoadAcquisitionTimesAsync(uid, GuideCardPools.Weapons, token);
        if (token.IsCancellationRequested || version != loadVersion || SelectedUser?.Uid != uid) return;
        hasRoleGachaRecords = roleRecords;
        hasWeaponGachaRecords = weaponRecords;
        if (roleSortKey == "time" && !hasRoleGachaRecords) roleSortKey = "star";
        if (weaponSortKey == "time" && !hasWeaponGachaRecords) weaponSortKey = "star";
        RefreshSortOptions();
        lastSyncedAtUtc = snapshot.LastSyncedAtUtc;
        string language = LanguageManager.Instance.CurrentLanguage.GetCode();
        Roles.Clear();
        foreach (GuideRoleData role in snapshot.Roles)
        {
            int resourceId = ParseResourceId(role.RoleGbId);
            var item = resourceId == 0 ? null : gameDataService.GetItemById(resourceId);
            bool isLimitedFiveStar = role.Star == 5 && gameDataService.GetLimitedStatus(resourceId, role.Star, true) == true;
            DateTime? acquiredAt = isLimitedFiveStar && roleTimes.TryGetValue(resourceId, out DateTime roleTime) ? roleTime : null;
            string displayName = GetItemName(role.RoleGbId, language);
            int sequence = ResolveSequence(role);
            GuideRoleHoverDetails hover = GuideRoleHoverMapper.Map(role.DetailJson,
                snapshot.Weapons.FirstOrDefault(x => x.OwnerRoleGbId == role.RoleGbId),
                gameDataService, language, ex => logger.Warn("角色悬浮详情快照无法解析。", ex));
            Roles.Add(new GuideRoleCardViewModel
            {
                RoleGbId = role.RoleGbId,
                DisplayName = displayName,
                IconPath = $"Local/Icons/{role.RoleGbId}.png",
                Star = role.Star,
                IsLimitedFiveStar = isLimitedFiveStar,
                FirstAcquiredAt = acquiredAt,
                ToolTipText = FormatToolTip(displayName, acquiredAt),
                PortraitPath = CatalogJson.IsImageUrl(role.IllustrationPictureUrl) ? role.IllustrationPictureUrl : null,
                Attributes = hover.Attributes,
                EquippedWeapon = hover.Weapon,
                AcquiredText = acquiredAt.HasValue
                    ? string.Format(LanguageManager.Instance["Guide_FirstAcquired"], acquiredAt.Value.ToString("yyyy-MM-dd HH:mm", CultureInfo.CurrentCulture))
                    : LanguageManager.Instance["Guide_AcquiredUnknown"],
                Sequence = sequence,
                SequenceLabel = FormatSequence(sequence),
                SourceOrder = role.SourceOrder
            });
        }
        Weapons.Clear();
        foreach (GuideWeaponData weapon in snapshot.Weapons)
        {
            int resourceId = ParseResourceId(weapon.WeaponGbId);
            var item = resourceId == 0 ? null : gameDataService.GetItemById(resourceId);
            bool isLimitedFiveStar = weapon.Star == 5 && gameDataService.GetLimitedStatus(resourceId, weapon.Star, false) == true;
            DateTime? acquiredAt = isLimitedFiveStar && weaponTimes.TryGetValue(resourceId, out DateTime weaponTime) ? weaponTime : null;
            string displayName = GetItemName(weapon.WeaponGbId, language);
            Weapons.Add(new GuideWeaponCardViewModel
            {
                WeaponGbId = weapon.WeaponGbId,
                DisplayName = displayName,
                OwnerName = GetItemName(weapon.OwnerRoleGbId, language),
                ImageUrl = weapon.PictureUrl,
                Star = weapon.Star,
                IsLimitedFiveStar = isLimitedFiveStar,
                FirstAcquiredAt = acquiredAt,
                ToolTipText = FormatToolTip(displayName, acquiredAt),
                SourceOrder = weapon.SourceOrder
            });
        }
        SortRoles();
        SortWeapons();
        RaiseCounts();
        _ = PrepareHoverImagesAsync(token);
    }

    /// <summary>在页面加载后预备静态图片，不因鼠标悬停触发请求。</summary>
    private async Task PrepareHoverImagesAsync(CancellationToken token)
    {
        if (imageService is null) return;
        string[] paths = Roles.SelectMany(x => new[] { x.PortraitPath, x.EquippedWeapon?.ImagePath })
            .OfType<string>().ToArray();
        try { await Task.Run(() => imageService.PrepareHoverImagesAsync(paths, token), token); }
        catch (OperationCanceledException) { /* 页面离开或账号切换取消预载。 */ }
        catch (Exception ex) { logger.Warn("角色悬浮卡片图片预载失败，使用已有缓存或头像。", ex); }
    }

    /// <summary>手动同步或清空缓存后，为仍在显示的账号重新准备图片，不重新请求账号资料。</summary>
    private void OnImageRetryRequested(object? sender, EventArgs e)
    {
        System.Windows.Application.Current.Dispatcher.InvokeAsync(async () =>
        {
            if (SelectedUser is null || navigationCts.IsCancellationRequested) return;
            await PrepareHoverImagesAsync(navigationCts.Token);
        });
    }

    private async void Sync()
    {
        if (SelectedUser is null || IsBusy) return;
        string uid = SelectedUser.Uid;
        CancellationToken token = navigationCts.Token;
        IsBusy = true;
        uiStateService.ShowLoading(LanguageManager.Instance["Guide_Syncing"]);
        try
        {
            string language = LanguageManager.Instance.CurrentLanguage.GetCode();
            try
            {
                await guideSyncService.SyncAsync(uid, language, token);
            }
            catch (GuideAuthenticationRequiredException)
            {
                bool captured = await TryCaptureCurrentSessionAsync(language, token);
                if (captured)
                {
                    try
                    {
                        await guideSyncService.SyncAsync(uid, language, token);
                    }
                    catch (GuideAuthenticationRequiredException)
                    {
                        captured = false;
                    }
                }
                if (!captured)
                {
                    uiStateService.HideLoading();
                    bool loginSucceeded = await ShowLoginAsync();
                    if (!loginSucceeded) return;
                    uiStateService.ShowLoading(LanguageManager.Instance["Guide_Syncing"]);
                    if (!await TryCaptureCurrentSessionAsync(language, token))
                        throw new GuideAuthenticationRequiredException(LanguageManager.Instance["Guide_LoginRequired"]);
                    await guideSyncService.SyncAsync(uid, language, token);
                }
            }
            if (token.IsCancellationRequested || SelectedUser?.Uid != uid) return;
            await LoadSnapshotAsync(uid);
            uiStateService.ShowToast(LanguageManager.Instance["Toast_Success"], LanguageManager.Instance["Guide_SyncSuccess"], NotificationType.Success);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            logger.Error("Guide 角色数据同步失败", ex);
            uiStateService.ShowToast(LanguageManager.Instance["Toast_Error"], ex.Message, NotificationType.Error);
        }
        finally
        {
            uiStateService.HideLoading();
            IsBusy = false;
        }
    }

    private async Task<bool> TryCaptureCurrentSessionAsync(string language, CancellationToken token)
    {
        LoginContext context = loginService.LatestAuthenticatedContext;
        if (string.IsNullOrWhiteSpace(context.CUid) || string.IsNullOrWhiteSpace(context.AccessToken)) return false;
        try
        {
            await guideSyncService.CaptureSessionAsync(context.CUid, context.CName, context.AccessToken, language, token);
            return true;
        }
        catch (GuideAuthenticationRequiredException) { return false; }
        catch (GuideApiException ex)
        {
            logger.Debug("当前 SDK 登录上下文无法换取 Guide 令牌，将请求重新登录。", ex);
            return false;
        }
    }

    private Task<bool> ShowLoginAsync()
    {
        var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        dialogService.ShowDialog("LoginView", null, result => completion.TrySetResult(result.Result == ButtonResult.OK));
        return completion.Task;
    }

    private void SortRoles() => Replace(Roles, roleSortKey switch
    {
        "name" => GuideCardSortHelper.OrderByName(Roles, x => x.DisplayName),
        "time" => GuideCardSortHelper.OrderByAcquisitionTime(Roles),
        _ => GuideCardSortHelper.OrderByGlobalGroup(Roles)
    });

    private void SortWeapons() => Replace(Weapons, weaponSortKey switch
    {
        "name" => GuideCardSortHelper.OrderByName(Weapons, x => x.DisplayName),
        "time" => GuideCardSortHelper.OrderByAcquisitionTime(Weapons),
        _ => GuideCardSortHelper.OrderByGlobalGroup(Weapons)
    });

    private static void Replace<T>(ObservableCollection<T> target, IEnumerable<T> items)
    {
        T[] ordered = items.ToArray();
        target.Clear();
        foreach (T item in ordered) target.Add(item);
    }

    private string GetItemName(string id, string language) =>
        int.TryParse(id, out int resourceId) ? gameDataService.GetItemById(resourceId)?.GetName(language) ?? "None" : "None";

    private static int ParseResourceId(string id) => int.TryParse(id, out int resourceId) ? resourceId : 0;

    private string FormatToolTip(string displayName, DateTime? acquiredAt) => acquiredAt.HasValue
        ? $"{displayName}\n{string.Format(LanguageManager.Instance["Guide_FirstAcquired"], acquiredAt.Value.ToString("yyyy-MM-dd HH:mm", CultureInfo.CurrentCulture))}"
        : displayName;

    private async Task<(Dictionary<int, DateTime> Times, bool HasRecords)> LoadAcquisitionTimesAsync(
        string uid, IReadOnlyList<CardPoolType> poolTypes, CancellationToken token)
    {
        var result = await userDataService.ReadAcquisitionTimesAsync(uid, poolTypes.Select(x => (int)x).ToArray(), token);
        return (result.Times, result.HasRecords);
    }
    private string FormatSequence(int sequence)
    {
        if (sequence is < 0 or > 6)
        {
            logger.Warn($"Guide 返回了无效共鸣链数值：{sequence}");
            return sequence.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }
        return LanguageManager.Instance.CurrentLanguage switch
        {
            LanguageType.En => $"S{sequence}",
            LanguageType.Ja => $"{sequence}共鳴",
            _ => sequence == 0 ? "零链" : $"{ToChineseNumber(sequence)}链"
        };
    }

    private int ResolveSequence(GuideRoleData role)
    {
        if (role.Sequence is >= 0 and <= 6) return role.Sequence;
        if (!string.IsNullOrWhiteSpace(role.DetailJson))
        {
            try
            {
                GuideIntroductionDetail? detail = JsonSerializer.Deserialize<GuideIntroductionDetail>(role.DetailJson, new JsonSerializerOptions(JsonSerializerDefaults.Web));
                if (detail?.RoleResonance?.Items is { } items)
                    return Math.Clamp(items.Count(x => x.IsAcquired), 0, 6);
            }
            catch (JsonException ex)
            {
                logger.Warn($"无法从本地详情恢复角色 {role.RoleGbId} 的共鸣链。", ex);
            }
        }
        logger.Warn($"Guide 角色 {role.RoleGbId} 缺少有效共鸣链数据，按零链显示。");
        return 0;
    }

    private static string ToChineseNumber(int number) => number switch { 1 => "一", 2 => "二", 3 => "三", 4 => "四", 5 => "五", 6 => "六", _ => "零" };

    /// <summary>目录更新只重建本地展示，不触发账号网络同步。</summary>
    private void OnCatalogChanged(object? sender, EventArgs e)
    {
        System.Windows.Application.Current.Dispatcher.InvokeAsync(async () =>
        {
            if (SelectedUser is null || navigationCts.IsCancellationRequested) return;
            try { await LoadSnapshotAsync(SelectedUser.Uid); }
            catch (OperationCanceledException) { }
            catch (Exception ex) { logger.Warn("目录更新后刷新角色展示失败。", ex); }
        });
    }

    private async void OnLanguageChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != "Item[]") return;
        RefreshSortOptions();
        if (SelectedUser is not null && !navigationCts.IsCancellationRequested)
        {
            try { await LoadSnapshotAsync(SelectedUser.Uid); }
            catch (OperationCanceledException) { /* 页面切换取消刷新。 */ }
            catch (Exception ex) { logger.Warn("语言更新后刷新角色展示失败。", ex); }
        }
        RaisePropertyChanged(nameof(LastSyncedText));
    }

    private void RefreshSortOptions()
    {
        RoleSortOptions.Clear();
        RoleSortOptions.Add(new GuideSortOption("star", LanguageManager.Instance["Guide_SortStar"], true));
        RoleSortOptions.Add(new GuideSortOption("name", LanguageManager.Instance["Guide_SortName"], true));
        RoleSortOptions.Add(new GuideSortOption("time", LanguageManager.Instance["Guide_SortTime"], hasRoleGachaRecords, hasRoleGachaRecords ? null : LanguageManager.Instance["Guide_NoLocalGacha"]));
        WeaponSortOptions.Clear();
        WeaponSortOptions.Add(new GuideSortOption("star", LanguageManager.Instance["Guide_SortStar"], true));
        WeaponSortOptions.Add(new GuideSortOption("name", LanguageManager.Instance["Guide_SortName"], true));
        WeaponSortOptions.Add(new GuideSortOption("time", LanguageManager.Instance["Guide_SortTime"], hasWeaponGachaRecords, hasWeaponGachaRecords ? null : LanguageManager.Instance["Guide_NoLocalGacha"]));
        RaisePropertyChanged(nameof(SelectedRoleSort));
        RaisePropertyChanged(nameof(SelectedWeaponSort));
    }

    private void RaiseCounts()
    {
        RaisePropertyChanged(nameof(RoleCount));
        RaisePropertyChanged(nameof(WeaponCount));
        RaisePropertyChanged(nameof(HasRoles));
        RaisePropertyChanged(nameof(HasWeapons));
        RaisePropertyChanged(nameof(LastSyncedText));
    }
}

public static class GuideCardPools
{
    public static readonly IReadOnlyList<CardPoolType> Roles =
    [
        CardPoolType.CharacterEvent, CardPoolType.CharacterStandard, CardPoolType.Beginner,
        CardPoolType.BeginnerChoice, CardPoolType.CharacterNoviceJourney, CardPoolType.CharacterCollaboration
    ];

    public static readonly IReadOnlyList<CardPoolType> Weapons =
    [
        CardPoolType.WeaponEvent, CardPoolType.WeaponStandard,
        CardPoolType.WeaponNoviceJourney, CardPoolType.WeaponCollaboration
    ];
}

public sealed record GuideSortOption(string Key, string DisplayName, bool IsEnabled, string? DisabledHint = null);

public interface IGuideCard
{
    int Star { get; }
    bool IsLimitedFiveStar { get; }
    DateTime? FirstAcquiredAt { get; }
    int SourceOrder { get; }
}

public static class GuideCardSortHelper
{
    /// <summary>
    /// 所有展示排序均先固定为限定五星、常驻五星、四星，异常稀有度置于末尾。
    /// </summary>
    public static IReadOnlyList<T> OrderByGlobalGroup<T>(IEnumerable<T> items) where T : IGuideCard =>
        items.OrderBy(x => GetGlobalGroupRank(x))
            .ThenBy(x => x.SourceOrder)
            .ToArray();

    public static IReadOnlyList<T> OrderByName<T>(IEnumerable<T> items, Func<T, string> nameSelector) where T : IGuideCard =>
        items.OrderBy(x => GetGlobalGroupRank(x))
            .ThenBy(nameSelector, StringComparer.CurrentCulture)
            .ThenBy(x => x.SourceOrder)
            .ToArray();

    public static IReadOnlyList<T> OrderByAcquisitionTime<T>(IEnumerable<T> items) where T : IGuideCard =>
        items.OrderBy(x => GetGlobalGroupRank(x))
            .ThenBy(x => x.IsLimitedFiveStar && x.FirstAcquiredAt.HasValue ? 0 : 1)
            .ThenByDescending(x => x.IsLimitedFiveStar ? x.FirstAcquiredAt : null)
            .ThenBy(x => x.SourceOrder)
            .ToArray();

    private static int GetGlobalGroupRank(IGuideCard item) => item switch
    {
        { Star: 5, IsLimitedFiveStar: true } => 0,
        { Star: 5 } => 1,
        { Star: 4 } => 2,
        _ => 3
    };
}

public sealed class GuideRoleCardViewModel : IGuideCard
{
    public string? PortraitPath { get; init; }
    public IReadOnlyList<GuideHoverAttribute> Attributes { get; init; } = [];
    public GuideHoverWeapon? EquippedWeapon { get; init; }
    public bool HasAttributes => Attributes.Count > 0;
    public bool HasEquippedWeapon => EquippedWeapon is not null;
    public string AcquiredText { get; init; } = string.Empty;
    public string RoleGbId { get; init; } = string.Empty;
    public string DisplayName { get; init; } = "None";
    public string? IconPath { get; init; }
    public int Star { get; init; }
    public bool IsLimitedFiveStar { get; init; }
    public DateTime? FirstAcquiredAt { get; init; }
    public string ToolTipText { get; init; } = string.Empty;
    public int Sequence { get; init; }
    public string SequenceLabel { get; init; } = string.Empty;
    public int SourceOrder { get; init; }
}

public sealed class GuideWeaponCardViewModel : IGuideCard
{
    public string WeaponGbId { get; init; } = string.Empty;
    public string DisplayName { get; init; } = "None";
    public string OwnerName { get; init; } = "None";
    public string? ImageUrl { get; init; }
    public int Star { get; init; }
    public bool IsLimitedFiveStar { get; init; }
    public DateTime? FirstAcquiredAt { get; init; }
    public string ToolTipText { get; init; } = string.Empty;
    public int SourceOrder { get; init; }
    public string IconPath => $"Local/Icons/{WeaponGbId}.png";
}
