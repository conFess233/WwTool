using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WwTool.Core.Abstractions;
using WwTool.Core.Enums;
using WwTool.Core.Abstractions.Platform;
using WwTool.Core.Exceptions;
using WwTool.Core.Extensions;
using WwTool.Core.Localization;
using WwTool.Core.Models.Domain;
using WwTool.Core.Services;
using WwTool.UI.Feedback;
using WwTool.UI.Navigation;
using WwTool.UI.Pages.Pulls;
using WwTool.UI.Services;
using WwTool.UI.Shell;

namespace WwTool.UI.Pages.Home;

/// <summary>资源卡底部的一行键值。<see cref="IsLive"/> 为实时倒计时（逐秒滚动、不闪光）。</summary>
public sealed partial class KeyValueRow(bool isLive = false) : ObservableObject
{
    public bool IsLive { get; } = isLive;
    [ObservableProperty] private string _key = string.Empty;
    [ObservableProperty] private string _value = "--";
}

/// <summary>首页账号总览卡里的一张资源小卡（结晶波片、结晶单质、活跃度、先约电台、周本奖励）。</summary>
public sealed partial class ResourceCard(string titleKey, string icon, bool isWeekly = false) : ObservableObject
{
    public string TitleKey { get; } = titleKey;
    public string Icon { get; } = icon;
    public bool IsWeekly { get; } = isWeekly;
    public bool HasBar => !IsWeekly;

    [ObservableProperty] private string _title = string.Empty;
    [ObservableProperty] private string _value = "--";
    [ObservableProperty] private string? _suffix;
    [ObservableProperty] private double _current;
    [ObservableProperty] private double _maximum = 1;

    /// <summary>结晶波片达到上限：描边流光、外侧光晕呼吸、标签旁「已满」闪烁。</summary>
    [ObservableProperty] private bool _isFull;

    /// <summary>周本三段格子：已完成的格子点亮。</summary>
    [ObservableProperty] private IReadOnlyList<bool> _cells = [false, false, false];

    public KeyValueRow[] Rows { get; } = [new(isLive: titleKey == "Index_CrystalSheet"), new(), new()];

    public void SetRows(params (string Key, string Value)[] rows)
    {
        for (int i = 0; i < Rows.Length; i++)
        {
            Rows[i].Key = i < rows.Length ? rows[i].Key : string.Empty;
            Rows[i].Value = i < rows.Length ? rows[i].Value : string.Empty;
        }
    }
}

/// <summary>便捷操作卡的一个按钮。</summary>
public sealed partial class QuickAction(string automationId, string icon, IRelayCommand command, bool isPrimary = false) : ObservableObject
{
    public string AutomationId { get; } = automationId;
    public string Icon { get; } = icon;
    public IRelayCommand Command { get; } = command;
    public bool IsPrimary { get; } = isPrimary;

    [ObservableProperty] private string _title = string.Empty;

    /// <summary>进行中：图标换成加载形象（正在启动、正在同步）。</summary>
    [ObservableProperty] private bool _isBusy;

    /// <summary>成功：图标换对勾、底色转绿（已复制）。</summary>
    [ObservableProperty] private bool _isSuccess;

    /// <summary>游戏运行中：图标旁绿点闪烁。</summary>
    [ObservableProperty] private bool _isRunning;
}

/// <summary>公告列表的一行。</summary>
public sealed record AnnouncementRow(string KindText, AnnouncementKind Kind, string Title, string DateText, Uri? Link)
{
    public bool IsEvent => Kind == AnnouncementKind.Event;
    public bool IsNews => Kind == AnnouncementKind.News;
    public bool IsNotice => Kind == AnnouncementKind.Notice;
}

/// <summary>
/// 首页（定稿 FinalHome）：左栏「账号总览」与「便捷操作」，右栏「游戏公告」。
/// 资源数据来自本地游戏数据快照；结晶波片按 6 分钟 1 点在本地实时推算，倒计时逐秒滚动。
/// </summary>
public sealed partial class HomeViewModel : ObservableObject, INavigationAware, IFabProvider
{
    private const string Icons = "avares://WwTool/Assets/Images/";
    private const int WeeklyMax = 3;
    private const int WeeklyCost = 60;
    private const int MinutesPerWaveplate = 6;

    private readonly PlayerData _player;
    private readonly PullsSession _pulls;
    private readonly LocalizationService _localization;
    private readonly IUIStateService _uiState;
    private readonly ErrorReporter _errors;
    private readonly IConfigService _config;
    private readonly IAppLauncher _launcher;
    private readonly IAppPaths _paths;
    private readonly CatalogSyncService _catalogSync;
    private readonly GameLauncherService _game;
    private readonly ClipboardService _clipboard;
    private readonly IAnnouncementSource _announcementSource;
    private readonly DispatcherTimer _clock;
    private AnnouncementFeed _feed = AnnouncementFeed.Empty;
    private CancellationTokenSource? _copyReset;
    private bool _announcementsLoaded;

    /// <summary>本次运行已从接口刷新过的「区服 + 语言」，不重复请求。</summary>
    /// <summary>各区服与语言上次开始从接口获取公告的时间；超过 <see cref="AnnouncementRefreshInterval"/> 再获取一次。</summary>
    private readonly Dictionary<string, DateTime> _announcementsAttempted = [];

    /// <summary>运行中公告的重新获取间隔：游戏更新版本后不必重启程序也能看到新公告。</summary>
    private static readonly TimeSpan AnnouncementRefreshInterval = TimeSpan.FromMinutes(30);
    private int _announcementVersion;
    private LauncherRegion _shownRegion;

    public HomeViewModel(
        AccountContext accounts,
        PlayerData player,
        PullsSession pulls,
        LocalizationService localization,
        IUIStateService uiState,
        ErrorReporter errors,
        AccountActions actions,
        WwTool.UI.Navigation.INavigationService navigation,
        IConfigService config,
        IAppLauncher launcher,
        IAppPaths paths,
        CatalogSyncService catalogSync,
        GameLauncherService game,
        ClipboardService clipboard,
        IAnnouncementSource announcementSource)
    {
        Accounts = accounts;
        _player = player;
        _pulls = pulls;
        _localization = localization;
        _uiState = uiState;
        _errors = errors;
        _config = config;
        _launcher = launcher;
        _paths = paths;
        _catalogSync = catalogSync;
        _game = game;
        _clipboard = clipboard;
        _announcementSource = announcementSource;

        FabActions =
        [
            // 主动从服务器刷新当前账号的资料（与账号、探索、摩托页的「刷新数据」相同）。
            new FabAction("refresh-data", "Fab_RefreshData", Icons + "Refresh.png", new AsyncRelayCommand(() => player.SyncAsync()), localization),
            new FabAction("refresh", "Index_RefreshAccountList", "Icon.Sync", new AsyncRelayCommand(RefreshAccountsAsync), localization),
            new FabAction("add-account", "Index_AddAccount", "Icon.Plus", new AsyncRelayCommand(() => actions.AddAccountAsync()), localization),
        ];

        Launch = new QuickAction("home-launch", "Icon.Play", new RelayCommand(LaunchGame), isPrimary: true);
        CopyLink = new QuickAction("home-copy-link", "Icon.Link", new AsyncRelayCommand(CopyGachaLinkAsync));
        SyncCatalog = new QuickAction("home-sync-catalog", "Icon.Sync", new AsyncRelayCommand(SyncCatalogAsync));
        QuickActions =
        [
            Launch,
            new QuickAction("home-screenshots", "Icon.Camera", new RelayCommand(() => OpenGameFolder("Client", "Saved", "ScreenShot"))),
            new QuickAction("home-game-folder", "Icon.Folder", new RelayCommand(() => OpenGameFolder())),
            new QuickAction("home-log-folder", "Icon.Log", new RelayCommand(OpenLogFolder)),
            CopyLink,
            SyncCatalog,
            new QuickAction("home-data-folder", "Icon.Database", new RelayCommand(() => _launcher.OpenFolder(_paths.DataRoot))),
            new QuickAction("home-guide-site", "Icon.Globe", new RelayCommand(() => OpenUrl(_config.Api.Urls.GuideSiteUrl))),
        ];

        Filters =
        [
            new AnnouncementFilter(null, "Home_AnnAll") { IsSelected = true },
            new AnnouncementFilter(AnnouncementKind.Event, "Home_AnnEvent"),
            new AnnouncementFilter(AnnouncementKind.Notice, "Home_AnnNotice"),
            new AnnouncementFilter(AnnouncementKind.News, "Home_AnnNews"),
        ];
        foreach (AnnouncementFilter filter in Filters) filter.PropertyChanged += OnFilterChanged;

        // 倒计时与波片推算每秒刷新；只在页面可见时运行。
        _clock = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _clock.Tick += (_, _) => UpdateLive();

        accounts.PropertyChanged += OnAccountsChanged;
        _player.SnapshotChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(HasSnapshot));
            UpdateFirstSync();
            OnPropertyChanged(nameof(IsOffline));
            OnPropertyChanged(nameof(ShowNoSnapshot));
            Refresh();
        };
        AddAccountCommand = new AsyncRelayCommand(() => actions.AddAccountAsync());
        OnlyPullsCommand = new RelayCommand(() => navigation.NavigateTo("pulls"));
        RetrySyncCommand = new AsyncRelayCommand(_player.SyncAsync);
        _player.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(PlayerData.IsLoading)) OnPropertyChanged(nameof(ShowNoSnapshot));
            if (e.PropertyName is nameof(PlayerData.IsSyncing) or nameof(PlayerData.IsLoading)) UpdateFirstSync();
            if (e.PropertyName == nameof(PlayerData.IsOffline))
            {
                OnPropertyChanged(nameof(IsOffline));
                OnPropertyChanged(nameof(OfflineText));
            }
        };
        _game.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(GameLauncherService.State)) UpdateLaunchState();
        };
        _localization.PropertyChanged += (_, e) =>
        {
            // 国际服资讯按界面语言获取，换语言后重新加载。
            if (e.PropertyName == nameof(LocalizationService.CurrentLanguage) && _announcementsLoaded && Region == LauncherRegion.Global)
            {
                LoadAnnouncements();
                return;
            }
            if (e.PropertyName != LocalizationService.IndexerName) return;
            Refresh();
            UpdateQuickTitles();
            UpdateAnnouncements();
        };
        Refresh();
        UpdateQuickTitles();
    }

    public AccountContext Accounts { get; }
    public IReadOnlyList<FabAction> FabActions { get; }

    public ResourceCard Energy { get; } = new("Index_CrystalSheet", Icons + "Energy.png");
    public ResourceCard StoreEnergy { get; } = new("Index_CrystalElement", Icons + "StorgeEnergy.png");
    public ResourceCard Liveness { get; } = new("Home_ResLiveness", Icons + "Liveness.png");
    public ResourceCard Radio { get; } = new("Index_Radio", Icons + "BattlePass.png");
    public ResourceCard WeeklyBoss { get; } = new("Home_ResWeekly", Icons + "WeeklyInst.png", isWeekly: true);
    public IReadOnlyList<ResourceCard> Resources => [Energy, StoreEnergy, Liveness, Radio, WeeklyBoss];

    public QuickAction Launch { get; }
    public QuickAction CopyLink { get; }
    public QuickAction SyncCatalog { get; }
    public IReadOnlyList<QuickAction> QuickActions { get; }

    public IReadOnlyList<AnnouncementFilter> Filters { get; }
    public ObservableCollection<AnnouncementRow> AnnouncementRows { get; } = [];

    [ObservableProperty] private IReadOnlyList<AnnouncementBanner> _banners = [];
    [ObservableProperty] private bool _hasBanners;
    [ObservableProperty] private bool _hasAnnouncements;

    private PlayerSnapshot? Snapshot => _player.Snapshot;
    public bool HasSnapshot => _player.HasSnapshot;

    /// <summary>空状态「首次启动」：添加游戏账号。</summary>
    public IAsyncRelayCommand AddAccountCommand { get; }

    /// <summary>空状态「首次启动」：只看抽卡记录。</summary>
    public IRelayCommand OnlyPullsCommand { get; }

    /// <summary>网络异常横条「重试」。</summary>
    public IAsyncRelayCommand RetrySyncCommand { get; }

    /// <summary>空状态「首次同步中」：有账号、还没有任何本地快照、正在同步。</summary>
    public bool IsFirstSync => Accounts.HasAccount && !HasSnapshot && _player.IsSyncing;

    /// <summary>账号总览内容（身份行、资源卡、周本）：有账号且不在首次同步中。</summary>
    public bool ShowProfile => Accounts.HasAccount && !IsFirstSync;

    /// <summary>首次同步的估算进度（按约 10 秒逼近 95%，同步结束即消失）。</summary>
    [ObservableProperty] private double _firstSyncProgress;

    private DispatcherTimer? _firstSyncTimer;

    private void UpdateFirstSync()
    {
        OnPropertyChanged(nameof(IsFirstSync));
        OnPropertyChanged(nameof(ShowProfile));
        if (IsFirstSync && _firstSyncTimer is null)
        {
            var started = DateTime.UtcNow;
            FirstSyncProgress = 0;
            _firstSyncTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
            _firstSyncTimer.Tick += (_, _) =>
                FirstSyncProgress = 0.95 * (1 - Math.Exp(-(DateTime.UtcNow - started).TotalSeconds / 4));
            _firstSyncTimer.Start();
        }
        else if (!IsFirstSync && _firstSyncTimer is not null)
        {
            _firstSyncTimer.Stop();
            _firstSyncTimer = null;
        }
    }

    /// <summary>网络异常：上次同步连不上服务器，仍显示缓存数据。</summary>
    public bool IsOffline => _player.IsOffline && HasSnapshot;

    public string OfflineText => Accounts.Current?.LastSyncedAtUtc is { } at
        ? string.Format(_localization["Home_OfflineBanner"], at.ToLocalTime().ToString("MM/dd", System.Globalization.CultureInfo.InvariantCulture))
        : _localization["Home_OfflineBannerNoDate"];
    public bool ShowNoSnapshot => Accounts.HasAccount && !HasSnapshot && !_player.IsLoading;

    [ObservableProperty] private string _uidLine = string.Empty;

    /// <summary>刚复制过 UID：按钮图标换对勾，2 秒后还原。</summary>
    [ObservableProperty] private bool _isUidCopied;

    public string CopyUidTip => _localization[IsUidCopied ? "Home_Copied" : "Home_CopyUid"];

    partial void OnIsUidCopiedChanged(bool value) => OnPropertyChanged(nameof(CopyUidTip));

    private CancellationTokenSource? _uidCopyReset;

    /// <summary>复制当前账号的完整 UID。</summary>
    [RelayCommand]
    private async Task CopyUidAsync()
    {
        if (Accounts.Current is not { } account) return;
        try
        {
            await _clipboard.SetTextAsync(account.Uid);
        }
        catch (Exception ex)
        {
            _errors.Report(ex, _localization["Home_CopyUid"]);
            return;
        }

        _uidCopyReset?.Cancel();
        var cts = new CancellationTokenSource();
        _uidCopyReset = cts;
        IsUidCopied = true;
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(2), cts.Token);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        IsUidCopied = false;
    }
    [ObservableProperty] private string _activeDays = "--";
    [ObservableProperty] private string _createDate = "--";
    [ObservableProperty] private string _worldLevel = "--";
    [ObservableProperty] private string _server = "--";
    [ObservableProperty] private string _roleCount = "--";

    public void OnNavigatedTo(object? parameter)
    {
        _clock.Start();
        _game.Refresh();
        UpdateLive();
        _ = _player.EnsureLoadedAsync();
        if (!_announcementsLoaded) LoadAnnouncements();
        else RefreshAnnouncementsIfStale();
    }

    private static string AnnouncementKey(LauncherRegion region, string language) =>
        $"{region}|{LauncherNewsService.RequestLanguage(region, language)}";

    private bool IsAnnouncementStale(string key) =>
        !_announcementsAttempted.TryGetValue(key, out DateTime at) || DateTime.UtcNow - at >= AnnouncementRefreshInterval;

    /// <summary>首页显示中、上次获取已超过间隔时，在后台重新获取（不重读缓存，列表不闪动）。</summary>
    private void RefreshAnnouncementsIfStale()
    {
        if (!_announcementsLoaded || IsAnnouncementsLoading) return;
        LauncherRegion region = Region;
        if (region != _shownRegion) return;
        string language = _localization.CurrentLanguage.GetCode();
        string key = AnnouncementKey(region, language);
        if (!IsAnnouncementStale(key)) return;
        _announcementsAttempted[key] = DateTime.UtcNow;
        _ = RefreshAnnouncementsAsync(region, language, key, _announcementVersion, hasCache: true);
    }

    public void OnNavigatedFrom() => _clock.Stop();

    // ── 账号与资源 ──────────────────────────────────────────

    private async Task RefreshAccountsAsync()
    {
        try
        {
            await Accounts.RefreshAsync();
            int count = Accounts.Accounts.Count;
            _uiState.ShowToast(new NotificationRequest
            {
                Title = _localization[count == 0 ? "Toast_Info" : "Toast_Success"],
                Message = count == 0 ? _localization["Msg_ActionNoNewData"] : string.Format(_localization["Msg_ReadAccountsSuccess"], count),
                Type = count == 0 ? NotificationType.Info : NotificationType.Success,
                Priority = NotificationPriority.Important,
                Source = "Home",
                DedupeKey = "home-refresh-accounts",
            });
        }
        catch (Exception ex)
        {
            _errors.Report(ex, _localization["Index_RefreshAccountList"]);
        }
    }

    private void OnAccountsChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(AccountContext.HasAccount) or nameof(AccountContext.Current)) UpdateFirstSync();
        if (e.PropertyName == nameof(AccountContext.Current)) OnPropertyChanged(nameof(OfflineText));
        if (e.PropertyName != nameof(AccountContext.Current)) return;
        OnPropertyChanged(nameof(ShowNoSnapshot));
        Refresh();
        // 公告区服未手动选择时跟随账号，换到另一区服的账号后切换。
        if (_announcementsLoaded && Region != _shownRegion) OnRegionChanged();
    }

    private void Refresh()
    {
        AccountSummary? account = Accounts.Current;
        PlayerBaseSnapshot? b = Snapshot?.Base;
        string none = _localization["Common_Unavailable"];

        UidLine = account is null ? string.Empty : string.Format(_localization["Home_Uid"], account.Uid);
        ActiveDays = b is null ? none : b.ActiveDays.ToString(CultureInfo.CurrentCulture);
        CreateDate = b is null || b.CreatTime <= 0 ? none : FromTimestamp(b.CreatTime).LocalDateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        WorldLevel = b is null ? none : b.WorldLevel.ToString(CultureInfo.CurrentCulture);
        Server = string.IsNullOrWhiteSpace(account?.Region) ? none : account.Region;
        RoleCount = b is null ? none : string.Format(_localization["Home_RoleCount"], b.RoleNum);

        foreach (ResourceCard card in Resources) card.Title = _localization[card.TitleKey];

        UpdateLive();
        UpdateLiveness(b);
        UpdateRadio(b, Snapshot?.BattlePass);
        UpdateWeekly(b);
    }

    /// <summary>结晶波片与结晶单质：按回满时间戳实时推算当前值与倒计时。</summary>
    private void UpdateLive()
    {
        RefreshAnnouncementsIfStale();
        PlayerBaseSnapshot? b = Snapshot?.Base;
        string none = _localization["Common_Unavailable"];
        if (b is null)
        {
            SetValue(Energy, null, null);
            SetValue(StoreEnergy, null, null);
            Energy.IsFull = false;
            Energy.SetRows((_localization["Home_KvRecoverIn"], none), (_localization["Home_KvFullAt"], none), (_localization["Home_KvRate"], none));
            StoreEnergy.SetRows((_localization["Home_KvStatus"], none), (_localization["Home_KvSource"], none), (_localization["Home_KvRefillable"], none));
            return;
        }

        DateTimeOffset now = DateTimeOffset.Now;
        int energy = b.Energy;
        TimeSpan? left = null;
        DateTimeOffset? fullAt = null;
        if (b.EnergyRecoverTime > 0 && b.MaxEnergy > 0 && b.Energy < b.MaxEnergy)
        {
            fullAt = FromTimestamp(b.EnergyRecoverTime);
            left = fullAt.Value - now;
            if (left <= TimeSpan.Zero)
            {
                energy = b.MaxEnergy;
                left = null;
            }
            else
            {
                int missing = (int)Math.Ceiling(left.Value.TotalMinutes / MinutesPerWaveplate);
                energy = Math.Clamp(b.MaxEnergy - missing, b.Energy, b.MaxEnergy);
            }
        }

        bool full = b.MaxEnergy > 0 && energy >= b.MaxEnergy;
        SetValue(Energy, energy, b.MaxEnergy);
        Energy.IsFull = full;
        string countdown = full ? _localization["Home_AlreadyFull"]
            : left is { } l ? string.Format(CultureInfo.InvariantCulture, "{0:D2}:{1:D2}:{2:D2}", (int)l.TotalHours, l.Minutes, l.Seconds)
            : _localization["Conv_PauseRecover"];
        Energy.SetRows(
            (_localization["Home_KvRecoverIn"], countdown),
            (_localization["Home_KvFullAt"], full || fullAt is null ? "—" : FormatDay(fullAt.Value.LocalDateTime)),
            (_localization["Home_KvRate"], string.Format(_localization["Home_RateValue"], MinutesPerWaveplate)));

        SetValue(StoreEnergy, b.StoreEnergy, b.MaxStoreEnergy);
        bool storeGrowing = full && b.StoreEnergy < b.MaxStoreEnergy;
        StoreEnergy.SetRows(
            (_localization["Home_KvStatus"], _localization[storeGrowing ? "Home_Recovering" : "Conv_PauseRecover"]),
            (_localization["Home_KvSource"], _localization["Home_SourceOverflow"]),
            (_localization["Home_KvRefillable"], Math.Min(b.StoreEnergy, Math.Max(0, b.MaxEnergy - energy)).ToString(CultureInfo.CurrentCulture)));
    }

    private void UpdateLiveness(PlayerBaseSnapshot? b)
    {
        string none = _localization["Common_Unavailable"];
        if (b is null || !b.LivenessUnlock)
        {
            SetValue(Liveness, null, null);
            if (b is not null) Liveness.Value = _localization["Index_Locked"];
            Liveness.SetRows((_localization["Home_KvLeftToday"], none), (_localization["Home_KvStatus"], none), (_localization["Home_KvRefresh"], none));
            return;
        }
        SetValue(Liveness, b.Liveness, b.LivenessMaxCount);
        int left = Math.Max(0, b.LivenessMaxCount - b.Liveness);
        DateTime reset = DateTime.Today.AddHours(4);
        if (DateTime.Now >= reset) reset = reset.AddDays(1);
        Liveness.SetRows(
            (_localization["Home_KvLeftToday"], string.Format(_localization["Home_PointsValue"], left)),
            (_localization["Home_KvStatus"], _localization[left == 0 ? "Home_Done" : "Home_InProgress"]),
            (_localization["Home_KvRefresh"], FormatDay(reset)));
    }

    private void UpdateRadio(PlayerBaseSnapshot? b, BattlePassSnapshot? bp)
    {
        string none = _localization["Common_Unavailable"];
        Radio.Suffix = null;
        if (b is null || bp is null || !bp.IsUnlock)
        {
            Radio.Value = bp is { IsUnlock: false } ? _localization["Index_Locked"] : none;
            Radio.Current = 0;
            Radio.SetRows((_localization["Home_KvWeekExp"], none), (_localization["Home_KvLevelExp"], none), (_localization["Home_KvToNext"], none));
            return;
        }
        Radio.Value = "LV." + bp.Level.ToString(CultureInfo.InvariantCulture);
        Radio.Maximum = Math.Max(1, bp.WeekMaxExp);
        Radio.Current = bp.WeekExp;
        Radio.SetRows(
            (_localization["Home_KvWeekExp"], $"{bp.WeekExp} / {bp.WeekMaxExp}"),
            (_localization["Home_KvLevelExp"], $"{bp.Exp} / {bp.ExpLimit}"),
            (_localization["Home_KvToNext"], Math.Max(0, bp.ExpLimit - bp.Exp).ToString(CultureInfo.CurrentCulture)));
    }

    private void UpdateWeekly(PlayerBaseSnapshot? b)
    {
        string none = _localization["Common_Unavailable"];
        if (b is null)
        {
            SetValue(WeeklyBoss, null, null);
            WeeklyBoss.Cells = [false, false, false];
            WeeklyBoss.SetRows((_localization["Home_KvWeeklyLeft"], none), (_localization["Home_KvCost"], none), (_localization["Home_KvReset"], none));
            return;
        }
        int used = Math.Clamp(b.WeeklyInstCount, 0, WeeklyMax);
        SetValue(WeeklyBoss, used, WeeklyMax);
        WeeklyBoss.Cells = Enumerable.Range(0, WeeklyMax).Select(i => i < used).ToArray();
        WeeklyBoss.SetRows(
            (_localization["Home_KvWeeklyLeft"], string.Format(_localization["Home_TimesValue"], WeeklyMax - used)),
            (_localization["Home_KvCost"], string.Format(_localization["Home_CostValue"], WeeklyCost)),
            (_localization["Home_KvReset"], _localization["Home_ResetValue"]));
    }

    private void SetValue(ResourceCard card, int? value, int? maximum)
    {
        if (value is null || maximum is null)
        {
            card.Value = _localization["Common_Unavailable"];
            card.Suffix = null;
            card.Current = 0;
            return;
        }
        card.Value = value.Value.ToString(CultureInfo.CurrentCulture);
        card.Suffix = "/ " + maximum.Value.ToString(CultureInfo.CurrentCulture);
        card.Maximum = Math.Max(1, maximum.Value);
        card.Current = value.Value;
    }

    // ── 便捷操作 ────────────────────────────────────────────

    private void UpdateQuickTitles()
    {
        string[] keys = ["Home_LaunchGame", "Home_ScreenshotFolder", "Home_GameFolder", "Home_LogFolder",
            "Home_CopyGachaLink", "Home_SyncCatalog", "Home_DataFolder", "Home_GuideSite"];
        for (int i = 0; i < QuickActions.Count; i++) QuickActions[i].Title = _localization[keys[i]];
        UpdateLaunchState();
        if (CopyLink.IsSuccess) CopyLink.Title = _localization["Home_Copied"];
        if (SyncCatalog.IsBusy) SyncCatalog.Title = _localization["Home_SyncingCatalog"];
    }

    private void UpdateLaunchState()
    {
        GameRunState state = _game.State;
        Launch.IsBusy = state == GameRunState.Launching;
        Launch.IsRunning = state == GameRunState.Running;
        Launch.Title = _localization[state switch
        {
            GameRunState.Launching => "Home_Launching",
            GameRunState.Running => "Home_GameRunning",
            _ => "Home_LaunchGame",
        }];
    }

    private void LaunchGame()
    {
        try
        {
            _game.Launch();
        }
        catch (Exception ex)
        {
            _errors.Report(ex, _localization["Home_LaunchGame"]);
        }
    }

    private void OpenGameFolder(params string[] parts)
    {
        string? root = _config.User.GamePath;
        if (string.IsNullOrWhiteSpace(root))
        {
            _errors.Report(new WwToolGamePathException(_localization["Msg_NoGamePath"]), _localization["Home_GameFolder"]);
            return;
        }
        OpenExisting(Path.Combine([root, .. parts]));
    }

    private void OpenLogFolder()
    {
        string? root = _config.User.GamePath;
        if (string.IsNullOrWhiteSpace(root))
        {
            _errors.Report(new WwToolGamePathException(_localization["Msg_NoGamePath"]), _localization["Home_LogFolder"]);
            return;
        }
        string relative = _config.App.GameLogPath.Replace('\\', Path.DirectorySeparatorChar).TrimEnd(Path.DirectorySeparatorChar);
        OpenExisting(Path.Combine(root, relative));
    }

    // 游戏相关目录不存在时只提示，不替用户创建。
    private void OpenExisting(string folder)
    {
        if (!Directory.Exists(folder))
        {
            _uiState.ShowToast(_localization["Toast_Warning"], string.Format(_localization["Home_FolderMissing"], folder), NotificationType.Warning);
            return;
        }
        _launcher.OpenFolder(folder);
    }

    private void OpenUrl(string url)
    {
        if (Uri.TryCreate(url, UriKind.Absolute, out Uri? uri)) _launcher.OpenUrl(uri);
    }

    private async Task CopyGachaLinkAsync()
    {
        string? url = await _pulls.ReadUrlFromGameLogAsync();
        if (url is null) return;
        try
        {
            await _clipboard.SetTextAsync(url);
        }
        catch (Exception ex)
        {
            _errors.Report(ex, _localization["Home_CopyGachaLink"]);
            return;
        }

        // 已复制：图标换对勾、文字变「已复制」、底色转绿，2 秒后还原。
        _copyReset?.Cancel();
        var cts = new CancellationTokenSource();
        _copyReset = cts;
        CopyLink.IsSuccess = true;
        CopyLink.Title = _localization["Home_Copied"];
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(2), cts.Token);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        CopyLink.IsSuccess = false;
        CopyLink.Title = _localization["Home_CopyGachaLink"];
    }

    private async Task SyncCatalogAsync()
    {
        if (SyncCatalog.IsBusy || _catalogSync.IsBusy) return;
        SyncCatalog.IsBusy = true;
        SyncCatalog.Title = _localization["Home_SyncingCatalog"];
        try
        {
            await _catalogSync.SyncAsync(true);
            _uiState.ShowToast(_localization["Toast_Success"], _localization["Home_SyncCatalogDone"], NotificationType.Success);
        }
        catch (Exception ex)
        {
            _errors.Report(ex, _localization["Home_SyncCatalog"]);
        }
        finally
        {
            SyncCatalog.IsBusy = false;
            SyncCatalog.Title = _localization["Home_SyncCatalog"];
        }
    }

    // ── 游戏公告 ────────────────────────────────────────────

    /// <summary>
    /// 公告区服：设置里记住了就用记住的；否则按当前账号（UID 小于 2 亿为国服），没有账号时简体中文界面用国服、其他用国际服。
    /// </summary>
    public LauncherRegion Region
    {
        get
        {
            string? saved = _config.User.AnnouncementRegion;
            if (saved == "CN") return LauncherRegion.China;
            if (saved == "Global") return LauncherRegion.Global;
            if (Accounts.Current?.Uid is { } uid && long.TryParse(uid, out long id))
                return id < 200_000_000 ? LauncherRegion.China : LauncherRegion.Global;
            return _localization.CurrentLanguage == LanguageType.ZhHans ? LauncherRegion.China : LauncherRegion.Global;
        }
    }

    /// <summary>区服滑块：国服。</summary>
    public bool IsChinaRegion
    {
        get => Region == LauncherRegion.China;
        set { if (value) SetRegion(LauncherRegion.China); }
    }

    /// <summary>区服滑块：国际服。</summary>
    public bool IsGlobalRegion
    {
        get => Region == LauncherRegion.Global;
        set { if (value) SetRegion(LauncherRegion.Global); }
    }

    private void SetRegion(LauncherRegion region)
    {
        if (_config.User.AnnouncementRegion is not null && Region == region) return;
        bool changed = Region != region;
        _config.User.AnnouncementRegion = region == LauncherRegion.China ? "CN" : "Global";
        if (!changed) return;
        OnRegionChanged();
    }

    private void OnRegionChanged()
    {
        OnPropertyChanged(nameof(Region));
        OnPropertyChanged(nameof(IsChinaRegion));
        OnPropertyChanged(nameof(IsGlobalRegion));
        if (_announcementsLoaded) LoadAnnouncements();
    }

    [ObservableProperty] private bool _isAnnouncementsLoading;
    [ObservableProperty] private bool _announcementsFailed;

    /// <summary>先显示该区服的缓存，再在后台从接口刷新（同一区服与语言本次运行只刷新一次）。</summary>
    private void LoadAnnouncements()
    {
        _announcementsLoaded = true;
        int version = ++_announcementVersion;
        LauncherRegion region = Region;
        _shownRegion = region;
        string language = _localization.CurrentLanguage.GetCode();
        AnnouncementFeed? cached = null;
        try
        {
            cached = _announcementSource.LoadCached(region, language);
        }
        catch (Exception ex)
        {
            _errors.Report(ex, _localization["Home_Announcements"], notifyUser: false);
        }
        ApplyFeed(cached ?? AnnouncementFeed.Empty);
        AnnouncementsFailed = false;

        string key = AnnouncementKey(region, language);
        if (cached is not null && !IsAnnouncementStale(key)) return;
        _announcementsAttempted[key] = DateTime.UtcNow;
        _ = RefreshAnnouncementsAsync(region, language, key, version, hasCache: cached is not null);
    }

    private async Task RefreshAnnouncementsAsync(LauncherRegion region, string language, string key, int version, bool hasCache)
    {
        IsAnnouncementsLoading = !hasCache;
        AnnouncementFeed? fresh = null;
        try
        {
            fresh = await Task.Run(() => _announcementSource.RefreshAsync(region, language));
        }
        catch (Exception ex)
        {
            _errors.Report(ex, _localization["Home_Announcements"], notifyUser: false);
        }
        if (version != _announcementVersion) return;
        IsAnnouncementsLoading = false;
        if (fresh is not null)
        {
            ApplyFeed(fresh);
            // 轮播图在后台下载，下好后补上横幅。
            AnnouncementFeed? withImages = null;
            try
            {
                withImages = await Task.Run(() => _announcementSource.DownloadImagesAsync(region, language));
            }
            catch (Exception ex)
            {
                _errors.Report(ex, _localization["Home_Announcements"], notifyUser: false);
            }
            if (withImages is not null && version == _announcementVersion) ApplyFeed(withImages);
        }
        else if (!hasCache)
        {
            AnnouncementsFailed = true;
            OnPropertyChanged(nameof(ShowAnnouncementsEmpty));
        }
    }

    private void ApplyFeed(AnnouncementFeed feed)
    {
        _feed = feed;
        Banners = feed.Banners;
        HasBanners = Banners.Count > 0;
        UpdateAnnouncements();
    }

    private void OnFilterChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(AnnouncementFilter.IsSelected) || sender is not AnnouncementFilter { IsSelected: true } selected) return;
        foreach (AnnouncementFilter other in Filters)
        {
            if (!ReferenceEquals(other, selected)) other.IsSelected = false;
        }
        UpdateAnnouncements();
    }

    private void UpdateAnnouncements()
    {
        foreach (AnnouncementFilter filter in Filters) filter.Title = _localization[filter.TitleKey];
        AnnouncementKind? kind = Filters.FirstOrDefault(f => f.IsSelected)?.Kind;
        AnnouncementRows.Clear();
        // 「全部」按日期从新到旧合并三个栏目；单个栏目保持接口顺序。
        IEnumerable<Announcement> items = _feed.Items.Where(i => kind is null || i.Kind == kind);
        if (kind is null) items = items.OrderByDescending(i => i.Date ?? DateTime.MinValue);
        foreach (Announcement item in items)
        {
            AnnouncementRows.Add(new AnnouncementRow(KindText(item.Kind), item.Kind, item.Title,
                item.Date?.ToString("MM/dd", CultureInfo.InvariantCulture) ?? item.DateText ?? string.Empty, item.Link));
        }
        HasAnnouncements = !_feed.IsEmpty;
        OnPropertyChanged(nameof(ShowAnnouncementsEmpty));
        OnPropertyChanged(nameof(ShowAnnouncementsLoading));
        OnPropertyChanged(nameof(ShowAnnouncementsFailed));
    }

    /// <summary>空状态：没有内容、不在加载、也没有失败（失败另有提示）。</summary>
    public bool ShowAnnouncementsEmpty => !HasAnnouncements && !IsAnnouncementsLoading && !AnnouncementsFailed;
    public bool ShowAnnouncementsLoading => !HasAnnouncements && IsAnnouncementsLoading;
    public bool ShowAnnouncementsFailed => !HasAnnouncements && !IsAnnouncementsLoading && AnnouncementsFailed;

    partial void OnIsAnnouncementsLoadingChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowAnnouncementsEmpty));
        OnPropertyChanged(nameof(ShowAnnouncementsLoading));
        OnPropertyChanged(nameof(ShowAnnouncementsFailed));
    }

    partial void OnAnnouncementsFailedChanged(bool value) => OnIsAnnouncementsLoadingChanged(value);

    [RelayCommand]
    private void RetryAnnouncements() => LoadAnnouncements();

    private string KindText(AnnouncementKind kind) => _localization[kind switch
    {
        AnnouncementKind.Event => "Home_AnnEvent",
        AnnouncementKind.News => "Home_AnnNews",
        _ => "Home_AnnNotice",
    }];

    [RelayCommand]
    private void OpenAnnouncement(AnnouncementRow? row)
    {
        if (row?.Link is { } link) _launcher.OpenUrl(link);
    }

    [RelayCommand]
    private void OpenBanner(AnnouncementBanner? banner)
    {
        if (banner?.Link is { } link) _launcher.OpenUrl(link);
    }

    /// <summary>官网新闻页：国服；国际服按界面语言（简体中文界面用繁中页）。</summary>
    [RelayCommand]
    private void OpenAllAnnouncements()
    {
        if (Region == LauncherRegion.China)
        {
            OpenUrl(_config.Api.Urls.NewsPageUrlCN);
            return;
        }
        string site = _localization.CurrentLanguage switch
        {
            LanguageType.En => "en",
            LanguageType.Ja => "ja",
            _ => "zh-tw",
        };
        OpenUrl(string.Format(CultureInfo.InvariantCulture, _config.Api.Urls.NewsPageUrlNET, site));
    }

    // ── 格式 ────────────────────────────────────────────────

    /// <summary>「今天 10:26」「明天 10:26」，更远的日子显示「11-14 10:26」。</summary>
    private string FormatDay(DateTime local)
    {
        string time = local.ToString("HH:mm", CultureInfo.InvariantCulture);
        if (local.Date == DateTime.Today) return string.Format(_localization["Home_Today"], time);
        if (local.Date == DateTime.Today.AddDays(1)) return string.Format(_localization["Home_Tomorrow"], time);
        return local.ToString("MM-dd HH:mm", CultureInfo.InvariantCulture);
    }

    // 接口里秒级与毫秒级时间戳都有。
    private static DateTimeOffset FromTimestamp(long value) =>
        value > 20_000_000_000 ? DateTimeOffset.FromUnixTimeMilliseconds(value) : DateTimeOffset.FromUnixTimeSeconds(value);

}

/// <summary>公告筛选胶囊。</summary>
public sealed partial class AnnouncementFilter(AnnouncementKind? kind, string titleKey) : ObservableObject
{
    public AnnouncementKind? Kind { get; } = kind;
    public string TitleKey { get; } = titleKey;
    [ObservableProperty] private string _title = string.Empty;
    [ObservableProperty] private bool _isSelected;
}
