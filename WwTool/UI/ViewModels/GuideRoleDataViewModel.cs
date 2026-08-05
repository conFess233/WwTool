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
using WwTool.Common.Models.Entities;
using WwTool.Common.Utils;
using WwTool.Extensions;
using WwTool.Services;
using WwTool.Services.Interfaces;

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
    private CancellationTokenSource navigationCts = new();
    private bool isSelectingInitialAccount;
    private bool isBusy;
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
        ILoggerService logger)
    {
        this.userDataService = userDataService;
        this.guideRepository = guideRepository;
        this.guideSyncService = guideSyncService;
        this.loginService = loginService;
        this.configService = configService;
        this.uiStateService = uiStateService;
        this.dialogService = dialogService;
        this.gameDataService = gameDataService;
        this.logger = logger;
        SyncCommand = new DelegateCommand(Sync, () => !IsBusy && SelectedUser is not null)
            .ObservesProperty(() => IsBusy).ObservesProperty(() => SelectedUser);
        RefreshSortOptions();
        LanguageManager.Instance.PropertyChanged += OnLanguageChanged;
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
            if (!SetProperty(ref selectedUser, value) || value is null || isSelectingInitialAccount)
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
            IReadOnlyList<AccountSummary> accounts = await userDataService.ListAccountsAsync(navigationCts.Token);
            isSelectingInitialAccount = true;
            Users.Clear();
            foreach (AccountSummary account in accounts) Users.Add(account);
            SelectedUser = Users.FirstOrDefault(x => x.Uid == configService.User.LastUserId) ?? Users.FirstOrDefault();
            isSelectingInitialAccount = false;
            if (SelectedUser is not null) await LoadSnapshotAsync(SelectedUser.Uid);
        }
        catch (OperationCanceledException) { }
        finally { isSelectingInitialAccount = false; }
    }

    private async Task SelectAccountAsync(AccountSummary account)
    {
        configService.User.LastUserId = account.Uid;
        await configService.SaveAllAsync();
        loginService.SwitchUserContext(account.Uid);
        await LoadSnapshotAsync(account.Uid);
    }

    private async Task LoadSnapshotAsync(string uid)
    {
        GuideSnapshot snapshot = await guideRepository.LoadSnapshotAsync(uid, navigationCts.Token);
        (Dictionary<int, DateTime> roleTimes, bool roleRecords) = await LoadAcquisitionTimesAsync(uid, GuideCardPools.Roles);
        (Dictionary<int, DateTime> weaponTimes, bool weaponRecords) = await LoadAcquisitionTimesAsync(uid, GuideCardPools.Weapons);
        hasRoleGachaRecords = roleRecords;
        hasWeaponGachaRecords = weaponRecords;
        if (roleSortKey == "time" && !hasRoleGachaRecords) roleSortKey = "star";
        if (weaponSortKey == "time" && !hasWeaponGachaRecords) weaponSortKey = "star";
        RefreshSortOptions();
        lastSyncedAtUtc = snapshot.LastSyncedAtUtc;
        string language = LanguageManager.Instance.CurrentLanguage.GetCode();
        Roles.Clear();
        foreach (GuideRoleSnapshot role in snapshot.Roles)
        {
            int resourceId = ParseResourceId(role.RoleGbId);
            var item = resourceId == 0 ? null : gameDataService.GetItemById(resourceId);
            bool isLimitedFiveStar = role.Star == 5 && item?.IsUp == true;
            DateTime? acquiredAt = isLimitedFiveStar && roleTimes.TryGetValue(resourceId, out DateTime roleTime) ? roleTime : null;
            string displayName = GetItemName(role.RoleGbId, language);
            int sequence = ResolveSequence(role);
            Roles.Add(new GuideRoleCardViewModel
            {
                RoleGbId = role.RoleGbId,
                DisplayName = displayName,
                IconPath = item is null
                    ? role.CardPictureUrl
                    : $"Local/Icons/{role.RoleGbId}.png",
                Star = role.Star,
                IsLimitedFiveStar = isLimitedFiveStar,
                FirstAcquiredAt = acquiredAt,
                ToolTipText = FormatToolTip(displayName, acquiredAt),
                Sequence = sequence,
                SequenceLabel = FormatSequence(sequence),
                SourceOrder = role.SourceOrder
            });
        }
        Weapons.Clear();
        foreach (GuideEquippedWeaponSnapshot weapon in snapshot.Weapons)
        {
            int resourceId = ParseResourceId(weapon.WeaponGbId);
            var item = resourceId == 0 ? null : gameDataService.GetItemById(resourceId);
            bool isLimitedFiveStar = weapon.Star == 5 && item?.IsUp == true;
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
    }

    private async void Sync()
    {
        if (SelectedUser is null || IsBusy) return;
        IsBusy = true;
        uiStateService.ShowLoading(LanguageManager.Instance["Guide_Syncing"]);
        try
        {
            string language = LanguageManager.Instance.CurrentLanguage.GetCode();
            try
            {
                await guideSyncService.SyncAsync(SelectedUser.Uid, language, navigationCts.Token);
            }
            catch (GuideAuthenticationRequiredException)
            {
                bool captured = await TryCaptureCurrentSessionAsync(language);
                if (captured)
                {
                    try
                    {
                        await guideSyncService.SyncAsync(SelectedUser.Uid, language, navigationCts.Token);
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
                    if (!await TryCaptureCurrentSessionAsync(language))
                        throw new GuideAuthenticationRequiredException(LanguageManager.Instance["Guide_LoginRequired"]);
                    await guideSyncService.SyncAsync(SelectedUser.Uid, language, navigationCts.Token);
                }
            }
            await LoadSnapshotAsync(SelectedUser.Uid);
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

    private async Task<bool> TryCaptureCurrentSessionAsync(string language)
    {
        LoginContext context = loginService.LatestAuthenticatedContext;
        if (string.IsNullOrWhiteSpace(context.CUid) || string.IsNullOrWhiteSpace(context.AccessToken)) return false;
        try
        {
            await guideSyncService.CaptureSessionAsync(context.CUid, context.CName, context.AccessToken, language, navigationCts.Token);
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
        string uid, IReadOnlyList<CardPoolType> poolTypes)
    {
        IReadOnlyList<GachaData>[] pools = await Task.WhenAll(poolTypes.Select(type =>
            userDataService.ReadGachaInSourceOrderAsync(uid, (int)type, navigationCts.Token)));
        var times = new Dictionary<int, DateTime>();
        bool hasRecords = pools.Any(x => x.Count > 0);
        foreach (GachaData record in pools.SelectMany(x => x))
        {
            if (!DateTime.TryParse(record.Time, CultureInfo.CurrentCulture, DateTimeStyles.AllowWhiteSpaces, out DateTime parsed))
                continue;
            if (!times.TryGetValue(record.ResourceId, out DateTime existing) || parsed < existing)
                times[record.ResourceId] = parsed;
        }
        return (times, hasRecords);
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

    private int ResolveSequence(GuideRoleSnapshot role)
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

    private void OnLanguageChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != "Item[]") return;
        RefreshSortOptions();
        if (SelectedUser is not null) _ = LoadSnapshotAsync(SelectedUser.Uid);
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
