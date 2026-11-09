using System.ComponentModel;
using System.Globalization;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WwTool.Core.Abstractions;
using WwTool.Core.Enums;
using WwTool.Core.Localization;
using WwTool.Core.Models.Domain;
using WwTool.Core.Services.Gacha;
using WwTool.UI.Feedback;
using WwTool.UI.Navigation;
using WwTool.UI.Pages.Pulls;
using WwTool.UI.Services;
using WwTool.UI.Shell;

namespace WwTool.UI.Pages.Home;

/// <summary>首页的一张数值卡（体力、储备体力、活跃度、电台、周本）。</summary>
public sealed partial class GaugeCard(string titleKey, string icon) : ObservableObject
{
    public string TitleKey { get; } = titleKey;
    public string Icon { get; } = icon;

    [ObservableProperty] private string _title = string.Empty;
    [ObservableProperty] private string _value = "--";
    [ObservableProperty] private string? _suffix;
    [ObservableProperty] private double _current;
    [ObservableProperty] private double _maximum = 1;
    [ObservableProperty] private bool _hasBar = true;
    [ObservableProperty] private bool _isHot;
    [ObservableProperty] private string? _caption;
}

/// <summary>
/// 首页：当前账号概况、体力与周常、抽卡摘要。数据来自本地快照，“同步”从服务器拉取后刷新。
/// 账号由页头胶囊切换（<see cref="AccountContext"/>），抽卡摘要与抽卡统计页共享 <see cref="PullsSession"/>。
/// </summary>
public sealed partial class HomeViewModel : ObservableObject, INavigationAware, IFabProvider
{
    private const string Icons = "avares://WwTool/Assets/Images/";

    private readonly IUserDataService _userData;
    private readonly IGetDataService _getData;
    private readonly PullsSession _pulls;
    private readonly LocalizationService _localization;
    private readonly IUIStateService _uiState;
    private readonly ErrorReporter _errors;
    private readonly INavigationService _navigation;
    private readonly DispatcherTimer _clock;
    private CancellationTokenSource? _loadCts;

    public HomeViewModel(
        AccountContext accounts,
        IUserDataService userData,
        IGetDataService getData,
        PullsSession pulls,
        LocalizationService localization,
        IUIStateService uiState,
        ErrorReporter errors,
        INavigationService navigation)
    {
        Accounts = accounts;
        _userData = userData;
        _getData = getData;
        _pulls = pulls;
        _localization = localization;
        _uiState = uiState;
        _errors = errors;
        _navigation = navigation;

        FabActions =
        [
            new FabAction("refresh", "Index_RefreshAccountList", Icons + "Refresh.png", new AsyncRelayCommand(RefreshAccountsAsync), localization),
            new FabAction("add-account", "Index_AddAccount", Icons + "Add.png", new RelayCommand(ShowLoginMigrating), localization),
        ];

        // 回满倒计时每秒刷新；只在页面可见时运行。
        _clock = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _clock.Tick += (_, _) => UpdateCountdowns();

        accounts.PropertyChanged += OnAccountsChanged;
        _pulls.SnapshotReplaced += (_, _) => UpdatePulls();
        _localization.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == LocalizationService.IndexerName) Refresh();
        };
        Refresh();
    }

    public AccountContext Accounts { get; }
    public IReadOnlyList<FabAction> FabActions { get; }

    public GaugeCard Energy { get; } = new("Index_CrystalSheet", Icons + "Energy.png");
    public GaugeCard StoreEnergy { get; } = new("Index_CrystalElement", Icons + "StorgeEnergy.png");
    public GaugeCard Liveness { get; } = new("Index_Active", Icons + "Liveness.png");
    public GaugeCard Radio { get; } = new("Index_Radio", Icons + "BattlePass.png");
    public GaugeCard WeeklyBoss { get; } = new("Index_WeeklyBoss", Icons + "WeeklyInst.png");

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSnapshot), nameof(ShowNoSnapshot))]
    private PlayerSnapshot? _snapshot;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowNoSnapshot))]
    private bool _isLoading;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SyncCommand))]
    private bool _isSyncing;

    public bool HasSnapshot => Snapshot?.Base is not null;
    public bool ShowNoSnapshot => Accounts.HasAccount && !HasSnapshot && !IsLoading;

    [ObservableProperty] private string _uidLine = string.Empty;
    [ObservableProperty] private string _activeDays = "--";
    [ObservableProperty] private string _createDate = "--";
    [ObservableProperty] private string _worldLevel = "--";
    [ObservableProperty] private string _server = "--";
    [ObservableProperty] private string _roleCount = "--";

    [ObservableProperty] private bool _hasPulls;
    [ObservableProperty] private string _totalPulls = "--";
    [ObservableProperty] private string _fiveStars = "--";
    [ObservableProperty] private int _currentPity;
    [ObservableProperty] private string _pityText = string.Empty;

    public void OnNavigatedTo(object? parameter)
    {
        _clock.Start();
        if (Snapshot is null && Accounts.Current is not null) _ = LoadAsync();
        _ = _pulls.EnsureLoadedAsync();
    }

    public void OnNavigatedFrom() => _clock.Stop();

    /// <summary>从服务器同步当前账号的游戏数据（需要登录凭据），再读回本地快照。</summary>
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
        try
        {
            string? credential = await _userData.GetCredentialAsync(uid);
            await _getData.SyncAllUserDataAsync(uid, credential);
            await Accounts.RefreshAsync();
            if (Accounts.Current?.Uid != uid) return;
            await LoadAsync();
            if (HasSnapshot)
            {
                _uiState.ShowToast(new NotificationRequest
                {
                    Title = _localization["Toast_Success"],
                    Message = _localization["Msg_FetchRoleSuccess"],
                    Type = NotificationType.Success,
                    Priority = NotificationPriority.Important,
                    Source = "Home",
                    DedupeKey = "home-sync",
                });
            }
            else
            {
                _uiState.ShowToast(_localization["Toast_Warning"], _localization["Msg_FetchRoleEmpty"], NotificationType.Warning);
            }
        }
        catch (Exception ex)
        {
            _errors.Report(ex, _localization["Home_Sync"]);
        }
        finally
        {
            IsSyncing = false;
        }
    }

    private bool CanSync() => !IsSyncing;

    [RelayCommand]
    private void OpenPulls() => _navigation.NavigateTo("pulls");

    [RelayCommand]
    private void OpenRoles() => _navigation.NavigateTo("roles");

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

    // 登录对话框随账号页一起迁移；在此之前提示使用 1.x。
    private void ShowLoginMigrating() => _uiState.ShowToast(new NotificationRequest
    {
        Title = _localization["Index_AddAccount"],
        Message = _localization["Toast_FeatureMigrating"],
        Priority = NotificationPriority.Important,
        Source = "Home",
    });

    private void OnAccountsChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(AccountContext.Current)) return;
        OnPropertyChanged(nameof(ShowNoSnapshot));
        // 刷新账号列表会换成新的同 UID 对象；UID 没变时只更新页头文字，快照不必重读。
        if (Snapshot is not null && Accounts.Current?.Uid == _snapshotUid)
        {
            Refresh();
            return;
        }
        Snapshot = null;
        Refresh();
        if (Accounts.Current is not null) _ = LoadAsync();
    }

    private string? _snapshotUid;

    private async Task LoadAsync()
    {
        string? uid = Accounts.Current?.Uid;
        if (uid is null) return;
        _loadCts?.Cancel();
        var cts = new CancellationTokenSource();
        _loadCts = cts;
        IsLoading = true;
        try
        {
            PlayerSnapshot? snapshot = await _userData.LoadRoleSnapshotAsync(uid, cts.Token);
            if (cts.IsCancellationRequested || Accounts.Current?.Uid != uid) return;
            _snapshotUid = uid;
            Snapshot = snapshot;
            Refresh();
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested)
        {
            // 切换账号时取消旧的读取。
        }
        catch (Exception ex)
        {
            _errors.Report(ex, _localization["Msg_ReadingLocalRole"]);
        }
        finally
        {
            if (_loadCts == cts) IsLoading = false;
        }
    }

    private void Refresh()
    {
        AccountSummary? account = Accounts.Current;
        PlayerBaseSnapshot? b = Snapshot?.Base;
        string none = _localization["Common_Unavailable"];

        if (account is null)
        {
            UidLine = string.Empty;
        }
        else
        {
            string uid = MaskUid(account.Uid);
            UidLine = account.LastSyncedAtUtc is { } synced && b is not null
                ? string.Format(_localization["Home_UidSynced"], uid, FormatSyncTime(synced))
                : string.Format(_localization["Home_UidNotSynced"], uid);
        }

        ActiveDays = b is null ? none : b.ActiveDays.ToString(CultureInfo.CurrentCulture);
        CreateDate = b is null || b.CreatTime <= 0 ? none : FromTimestamp(b.CreatTime).LocalDateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        WorldLevel = b is null ? none : b.WorldLevel.ToString(CultureInfo.CurrentCulture);
        Server = string.IsNullOrWhiteSpace(account?.Region) ? none : account.Region;
        RoleCount = b is null ? none : string.Format(_localization["Home_RoleCount"], b.RoleNum);

        foreach (GaugeCard card in new[] { Energy, StoreEnergy, Liveness, Radio, WeeklyBoss })
        {
            card.Title = _localization[card.TitleKey];
        }

        SetGauge(Energy, b?.Energy, b?.MaxEnergy);
        SetGauge(StoreEnergy, b?.StoreEnergy, b?.MaxStoreEnergy);
        UpdateCountdowns();

        if (b is not null && !b.LivenessUnlock)
        {
            SetGauge(Liveness, null, null);
            Liveness.Caption = _localization["Index_Locked"];
        }
        else
        {
            SetGauge(Liveness, b?.Liveness, b?.LivenessMaxCount);
            if (b is not null)
            {
                int left = Math.Max(0, b.LivenessMaxCount - b.Liveness);
                Liveness.Caption = left == 0 ? _localization["Home_LivenessFull"] : string.Format(_localization["Home_LivenessLeft"], left);
            }
        }

        BattlePassSnapshot? bp = Snapshot?.BattlePass;
        Radio.Suffix = null;
        if (bp is null || b is null)
        {
            Radio.Value = none;
            Radio.Current = 0;
            Radio.Caption = null;
        }
        else if (!bp.IsUnlock)
        {
            Radio.Value = none;
            Radio.Current = 0;
            Radio.Caption = _localization["Index_Locked"];
        }
        else
        {
            Radio.Value = "LV." + bp.Level.ToString(CultureInfo.InvariantCulture);
            Radio.Suffix = null;
            Radio.Maximum = Math.Max(1, bp.WeekMaxExp);
            Radio.Current = bp.WeekExp;
            Radio.Caption = string.Format(_localization["Home_RadioWeek"], bp.WeekExp, bp.WeekMaxExp);
        }

        const int weeklyMax = 3;
        WeeklyBoss.HasBar = false;
        if (b is null)
        {
            WeeklyBoss.Value = none;
            WeeklyBoss.Suffix = null;
            WeeklyBoss.Caption = null;
        }
        else
        {
            WeeklyBoss.Value = b.WeeklyInstCount.ToString(CultureInfo.CurrentCulture);
            WeeklyBoss.Suffix = "/ " + weeklyMax;
            WeeklyBoss.Caption = string.Format(_localization["Home_WeeklyLeft"], Math.Max(0, weeklyMax - b.WeeklyInstCount));
        }

        UpdatePulls();
    }

    private void SetGauge(GaugeCard card, int? value, int? maximum)
    {
        if (value is null || maximum is null)
        {
            card.Value = _localization["Common_Unavailable"];
            card.Suffix = null;
            card.Current = 0;
            card.Caption = null;
            card.IsHot = false;
            return;
        }
        card.Value = value.Value.ToString(CultureInfo.CurrentCulture);
        card.Suffix = "/ " + maximum.Value.ToString(CultureInfo.CurrentCulture);
        card.Maximum = Math.Max(1, maximum.Value);
        card.Current = value.Value;
        card.IsHot = maximum.Value > 0 && value.Value >= maximum.Value;
    }

    private void UpdateCountdowns()
    {
        PlayerBaseSnapshot? b = Snapshot?.Base;
        Energy.Caption = b is null ? null : RecoverText(b.EnergyRecoverTime);
        StoreEnergy.Caption = b is null ? null : RecoverText(b.StoreEnergyRecoverTime);
    }

    private void UpdatePulls()
    {
        PullsSnapshot? snapshot = _pulls.Snapshot;
        HasPulls = snapshot is { IsEmpty: false } && snapshot.Uid == Accounts.Current?.Uid;
        if (!HasPulls || snapshot is null)
        {
            TotalPulls = FiveStars = _localization["Common_Unavailable"];
            CurrentPity = 0;
            PityText = string.Empty;
            return;
        }
        PullsSummary all = PullsSummaryCalculator.ForAll(snapshot);
        TotalPulls = all.TotalPulls.ToString("N0", CultureInfo.CurrentCulture);
        FiveStars = all.FiveStars.ToString(CultureInfo.CurrentCulture);
        CurrentPity = PullsSummaryCalculator.Progress(snapshot, CardPoolType.CharacterEvent).CurrentPity;
        PityText = string.Format(_localization["Home_CurrentPity"], CurrentPity);
    }

    /// <summary>回满倒计时；时间戳已过或为 0 表示暂停回复（与 1.x 一致）。</summary>
    private string RecoverText(long timestamp)
    {
        if (timestamp <= 0) return _localization["Conv_PauseRecover"];
        TimeSpan left = FromTimestamp(timestamp) - DateTimeOffset.Now;
        if (left <= TimeSpan.Zero) return _localization["Conv_PauseRecover"];
        return string.Format(_localization["Conv_RecoverAfter"],
            ((int)left.TotalHours).ToString("D2", CultureInfo.InvariantCulture),
            left.Minutes.ToString("D2", CultureInfo.InvariantCulture),
            left.Seconds.ToString("D2", CultureInfo.InvariantCulture));
    }

    // 接口里秒级与毫秒级时间戳都有。
    private static DateTimeOffset FromTimestamp(long value) =>
        value > 20_000_000_000 ? DateTimeOffset.FromUnixTimeMilliseconds(value) : DateTimeOffset.FromUnixTimeSeconds(value);

    private static string FormatSyncTime(DateTimeOffset utc)
    {
        DateTime local = utc.ToLocalTime().DateTime;
        return local.Date == DateTime.Today
            ? local.ToString("HH:mm", CultureInfo.InvariantCulture)
            : local.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
    }

    private static string MaskUid(string uid) => uid.Length <= 4 ? uid : uid[..4] + new string('●', uid.Length - 4);
}
