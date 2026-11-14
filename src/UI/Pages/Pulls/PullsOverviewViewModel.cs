using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WwTool.Core.Enums;
using WwTool.Core.Localization;
using WwTool.Core.Models.Domain;
using WwTool.Core.Services;
using WwTool.Core.Services.Gacha;
using WwTool.UI.Feedback;
using WwTool.UI.Navigation;
using WwTool.UI.Services;
using WwTool.UI.Shell;


namespace WwTool.UI.Pages.Pulls;

/// <summary>卡池页签。<see cref="Pool"/> 为 null 表示“总览”；<see cref="IsOther"/> 为“其他卡池”。</summary>
public sealed partial class PoolTab(CardPoolType? pool, bool isOther, string title) : ObservableObject
{
    public CardPoolType? Pool { get; } = pool;
    public bool IsOther { get; } = isOther;
    public string AutomationId => IsOther ? "pulls-tab-other" : Pool is null ? "pulls-tab-overview" : "pulls-tab-" + Pool;

    [ObservableProperty]
    private string _title = title;

    [ObservableProperty]
    private int _count;

    [ObservableProperty]
    private bool _isSelected;
}

/// <summary>总览「各卡池最近五星」的一行；卡池没有五星时 <see cref="Card"/> 为 null。</summary>
public sealed record LatestFiveStarRow(string PoolTitle, string Meta, FiveStarCard? Card)
{
    public bool HasCard => Card is not null;
    public bool NoCard => Card is null;
}

/// <summary>总览「未出金进度」的一组。</summary>
public sealed record PityProgressRow(string PoolTitle, int Pity, string ToHardPityText, string NextKindText)
{
    public bool HasNextKind => NextKindText.Length > 0;

    /// <summary>已垫 ≥ 70 抽：卡池名旁显示「即将触底」。</summary>
    public bool IsNearHardPity => Pity >= 70;
}

/// <summary>各卡池五星分布条。</summary>
public sealed record PoolShareBar(string PoolTitle, int FiveStars, double Ratio, string Text);

/// <summary>
/// 抽卡统计页：卡池页签、五张统计卡、总览内容，选中具体卡池时显示 <see cref="Records"/>。
/// 数据来自 <see cref="PullsSession"/>；本类只把快照转换为显示用的文字与列表。
/// </summary>
public sealed partial class PullsOverviewViewModel : ObservableObject, INavigationAware, IFabProvider
{
    private const int DailyWindowDays = 30;

    private readonly WwTool.Core.Abstractions.IConfigService _config;
    private readonly GamePathPicker _gamePath;
    private readonly PullsSession _session;
    private readonly LocalizationService _localization;
    private readonly INavigationService _navigation;
    private readonly GameDataService _gameData;
    private readonly ClipboardService _clipboard;
    private readonly IUIStateService _uiState;
    private readonly ErrorReporter _errors;

    public PullsOverviewViewModel(
        PullsSession session,
        GameDataService gameData,
        PoolRecordsViewModel records,
        AccountContext accounts,
        LocalizationService localization,
        INavigationService navigation,
        IDialogService dialogs,
        ClipboardService clipboard,
        IUIStateService uiState,
        ErrorReporter errors,
        GamePathPicker gamePath,
        PullsTimeline timeline,
        WwTool.Core.Abstractions.IConfigService config)
    {
        Timeline = timeline;
        _clipboard = clipboard;
        _uiState = uiState;
        _errors = errors;
        _gamePath = gamePath;
        _config = config;
        _session = session;
        _localization = localization;
        _navigation = navigation;
        _gameData = gameData;
        Records = records;
        Accounts = accounts;
        Session = session;

        // 悬浮按钮：扩展态「获取记录」，展开为获取云端数据（主）、加载本地数据、导入链接；清空数据已移到设置页。
        FabActions =
        [
            new FabAction("cloud", "Stat_GetCloudData", "Icon.Cloud",
                new AsyncRelayCommand(() => _session.GetRecordsAsync(() => dialogs.ShowAsync(new ImportUrlDialogViewModel(_session, localization, _session.Region)))),
                localization),
            new FabAction("load-local", "Stat_LoadLocalData", "Icon.FolderOpen", new AsyncRelayCommand(() => _session.ReloadAsync(showMessage: true)), localization),
            new FabAction("import-url", "Pulls_ImportUrl", "Icon.LinkAlt",
                new AsyncRelayCommand(async () =>
                {
                    if (await dialogs.ShowAsync(new ImportUrlDialogViewModel(_session, localization, _session.Region))) _session.ShowImported();
                }), localization),
        ];

        PickGamePathCommand = new AsyncRelayCommand(async () =>
        {
            if (await _gamePath.PickAsync()) Refresh();
        });

        Tabs.Add(new PoolTab(null, false, string.Empty));
        foreach (CardPoolType pool in PoolNames.MainPools) Tabs.Add(new PoolTab(pool, false, string.Empty));
        _otherTab = new PoolTab(null, true, string.Empty);
        Tabs[0].IsSelected = true;

        _session.SyncCompleted += (_, added) => ShowRefreshDone(added);
#if DEBUG
        // 本地截图验证：WWTOOL_DEMO_PULLS=drop | drop-done | refresh | refresh-done 时直接显示对应动效状态。
        switch (Environment.GetEnvironmentVariable("WWTOOL_DEMO_PULLS"))
        {
            case "drop": IsDropVisible = true; break;
            case "drop-done": IsDropVisible = true; IsDropBusy = true; DropDoneText = string.Format(localization["Pulls_DropImported"], 532); IsDropDone = true; break;
            case "refresh": IsRefreshVisible = true; break;
            case "refresh-done": RefreshDoneText = localization["Pulls_UpToDate"]; IsRefreshDone = true; IsRefreshVisible = true; break;
        }
#endif
        _session.SnapshotReplaced += (_, _) => Refresh();
        _session.PropertyChanged += OnSessionChanged;
        _localization.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName != LocalizationService.IndexerName) return;
            Refresh();
            Records.RefreshTexts();
        };
        Refresh();
    }

    public PullsSession Session { get; }

    // ── 动效「刷新完成」：获取云端数据时右下角浮出状态胶囊，完成后画出对勾、底色转绿 ──

    [ObservableProperty] private bool _isRefreshVisible;
    [ObservableProperty] private bool _isRefreshDone;
    [ObservableProperty] private string _refreshDoneText = string.Empty;
    private Avalonia.Threading.DispatcherTimer? _refreshHide;

    private void ShowRefreshDone(int added)
    {
        RefreshDoneText = added == 0 ? _localization["Pulls_UpToDate"] : string.Format(_localization["Pulls_RefreshAdded"], added);
        IsRefreshDone = true;
        IsRefreshVisible = true;
        _refreshHide?.Stop();
        _refreshHide = new Avalonia.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(2.4) };
        _refreshHide.Tick += (_, _) =>
        {
            _refreshHide?.Stop();
            IsRefreshVisible = false;
        };
        _refreshHide.Start();
    }

    // ── 动效「拖拽导入」：拖入链接或日志文件时显示虚线框，松手后变绿并显示导入条数 ──

    [ObservableProperty] private bool _isDropVisible;
    [ObservableProperty] private bool _isDropDone;
    [ObservableProperty] private bool _isDropBusy;
    [ObservableProperty] private string _dropDoneText = string.Empty;

    public void DragOver(bool entering)
    {
        if (IsDropBusy || IsDropDone) return;
        IsDropVisible = entering;
    }

    /// <summary>松手：导入并获取云端数据；成功后绿色「已导入 N 条记录」停留约 2 秒。</summary>
    public async Task DropAsync(string? text, string? filePath)
    {
        if (IsDropBusy) return;
        IsDropBusy = true;
        IsDropVisible = true;
        try
        {
            int? added = await _session.ImportDroppedAsync(text, filePath);
            if (added is not { } count)
            {
                IsDropVisible = false;
                return;
            }
            DropDoneText = string.Format(_localization["Pulls_DropImported"], count);
            IsDropDone = true;
            await Task.Delay(2200);
            IsDropVisible = false;
            await Task.Delay(300);
            IsDropDone = false;
        }
        finally
        {
            IsDropBusy = false;
        }
    }

    /// <summary>空状态「获取记录」。</summary>
    public System.Windows.Input.ICommand SyncCommand => FabActions[0].Command;

    /// <summary>空状态「手动粘贴链接」。</summary>
    public System.Windows.Input.ICommand ImportUrlCommand => FabActions[2].Command;

    /// <summary>空状态「选择游戏目录」。</summary>
    public IAsyncRelayCommand PickGamePathCommand { get; }

    /// <summary>没有记录且找不到游戏安装目录：先引导选择目录（获取记录要读游戏日志）。</summary>
    [ObservableProperty] private bool _showNoGamePath;

    /// <summary>没有记录、游戏目录正常：引导获取记录。</summary>
    [ObservableProperty] private bool _showNoRecords;

    public AccountContext Accounts { get; }

    public PoolRecordsViewModel Records { get; }

    /// <summary>时间线视图。</summary>
    public PullsTimeline Timeline { get; }

    public IReadOnlyList<FabAction> FabActions { get; }

    public FabTrigger? FabTrigger { get; } = new("Fab_GetRecords", "Icon.Cloud", ExecutesPrimary: false);

    /// <summary>获取中，悬浮按钮内显示加载形象。</summary>
    public bool IsFabBusy => _session.IsSyncing;

    /// <summary>总览 + 4 个主卡池 + （有记录时）其他卡池。</summary>
    public ObservableCollection<PoolTab> Tabs { get; } = [];

    private readonly PoolTab _otherTab;

    /// <summary>“其他卡池”页签下可选的卡池（5–13 中有记录的）。</summary>
    public ObservableCollection<PoolTab> OtherPools { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsOverview), nameof(IsPoolView), nameof(IsOtherSelected), nameof(ShowListPool), nameof(ShowCompactPool),
        nameof(ShowOverviewPage), nameof(IsCompactChecked))]
    private PoolTab? _selectedTab;

    public bool IsOverview => SelectedTab?.Pool is null && SelectedTab?.IsOther != true;
    public bool IsPoolView => !IsOverview;

    // —— 视图切换：列表 / 精简（头像格子，仅单卡池）/ 时间线，记住上次选择 ——

    public bool IsCompactView => _config.User.PullsCompactView;

    public bool IsTimelineView => _config.User.PullsTimelineView;

    /// <summary>「列表」按钮的选中态：总览时为总览页面本身。</summary>
    public bool IsListView => !IsTimelineView && (IsOverview || !IsCompactView);

    public bool IsCompactChecked => !IsTimelineView && IsPoolView && IsCompactView;

    public bool ShowOverviewPage => ShowContent && IsOverview && !IsTimelineView;
    public bool ShowListPool => ShowContent && IsPoolView && !IsCompactView && !IsTimelineView;
    public bool ShowCompactPool => ShowContent && IsPoolView && IsCompactView && !IsTimelineView;
    public bool ShowTimeline => ShowContent && IsTimelineView;

    [RelayCommand]
    private void SetViewMode(string mode)
    {
        _config.User.PullsTimelineView = mode == "timeline";
        if (mode != "timeline") _config.User.PullsCompactView = mode == "compact";
        NotifyViewMode();
    }

    private void NotifyViewMode()
    {
        foreach (string name in new[] { nameof(IsCompactView), nameof(IsTimelineView), nameof(IsListView), nameof(IsCompactChecked),
                     nameof(ShowOverviewPage), nameof(ShowListPool), nameof(ShowCompactPool), nameof(ShowTimeline) })
            OnPropertyChanged(name);
    }

    partial void OnShowContentChanged(bool value) => NotifyViewMode();
    public bool IsOtherSelected => SelectedTab?.IsOther == true;

    // —— 状态 ——
    [ObservableProperty] private bool _showNoAccount;
    [ObservableProperty] private bool _showEmpty;
    [ObservableProperty] private bool _showContent;
    [ObservableProperty] private string _storedSummary = string.Empty;

    // —— 五张统计卡 ——
    [ObservableProperty] private string _totalPulls = "0";
    [ObservableProperty] private string _totalPullsCaption = string.Empty;
    [ObservableProperty] private string _fiveStars = "0";
    [ObservableProperty] private string _fiveStarsCaption = string.Empty;
    [ObservableProperty] private string _averagePerFiveStar = "—";
    [ObservableProperty] private string _averagePerLimitedCaption = string.Empty;
    [ObservableProperty] private string _winRate = "—";
    [ObservableProperty] private string _winRateCaption = string.Empty;
    [ObservableProperty] private string _averagePerUp = "—";
    [ObservableProperty] private string _winRatio = string.Empty;
    [ObservableProperty] private string _astrites = "0";
    [ObservableProperty] private string _astritesCaption = string.Empty;

    // —— 总览内容 ——
    public ObservableCollection<LatestFiveStarRow> LatestRows { get; } = [];
    public ObservableCollection<PityProgressRow> PityRows { get; } = [];
    public ObservableCollection<PoolShareBar> ShareBars { get; } = [];
    [ObservableProperty] private IReadOnlyList<double> _dailyValues = [];
    [ObservableProperty] private IReadOnlyList<string> _dailyTips = [];
    [ObservableProperty] private IReadOnlyList<string> _dailyAxis = [];
    [ObservableProperty] private string _dailyCaption = string.Empty;
    [ObservableProperty] private IReadOnlyList<double> _winValues = [];
    [ObservableProperty] private double _winPercent;
    [ObservableProperty] private string _winLegend = string.Empty;
    [ObservableProperty] private string _lossLegend = string.Empty;
    [ObservableProperty] private IReadOnlyList<double> _fourStarValues = [];
    [ObservableProperty] private double _fourStarPercent;
    [ObservableProperty] private string _fourCharacterLegend = string.Empty;
    [ObservableProperty] private string _fourWeaponLegend = string.Empty;

    public void OnNavigatedTo(object? parameter) => _ = _session.EnsureLoadedAsync();

    public void OnNavigatedFrom() { }

    [RelayCommand]
    private void SelectTab(PoolTab tab)
    {
        foreach (PoolTab t in Tabs) t.IsSelected = ReferenceEquals(t, tab);
        SelectedTab = tab;
        if (tab.IsOther)
        {
            PoolTab? sub = OtherPools.FirstOrDefault(x => x.IsSelected) ?? OtherPools.FirstOrDefault();
            if (sub is not null) SelectOtherPool(sub);
        }
        else if (tab.Pool is { } pool)
        {
            Records.Pool = pool;
        }
        Refresh();
    }

    [RelayCommand]
    private void SelectOtherPool(PoolTab sub)
    {
        foreach (PoolTab t in OtherPools) t.IsSelected = ReferenceEquals(t, sub);
        if (sub.Pool is { } pool) Records.Pool = pool;
        Refresh();
    }

    [RelayCommand]
    private void OpenCharts() => _navigation.NavigateTo(PullsChartsViewModel.PageKey);

    [RelayCommand]
    private Task Retry() => _session.ReloadAsync(showMessage: true);

    private void OnSessionChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(PullsSession.Region) or nameof(PullsSession.Uid) or nameof(PullsSession.IsLoading)) Refresh();
        if (e.PropertyName == nameof(PullsSession.IsSyncing))
        {
            OnPropertyChanged(nameof(IsFabBusy));
            if (_session.IsSyncing)
            {
                _refreshHide?.Stop();
                IsRefreshDone = false;
                IsRefreshVisible = true;
            }
            else if (!IsRefreshDone)
            {
                IsRefreshVisible = false;
            }
        }
    }

    /// <summary>当前页签对应的卡池；总览为 null。</summary>
    private CardPoolType? CurrentPool => SelectedTab is { IsOther: true } ? OtherPools.FirstOrDefault(x => x.IsSelected)?.Pool : SelectedTab?.Pool;

    private void Refresh()
    {
        SelectedTab ??= Tabs[0];
        PullsSnapshot? s = _session.Snapshot;
        ShowNoAccount = _session.Uid is null;
        ShowEmpty = !ShowNoAccount && !_session.IsLoading && (s is null || s.IsEmpty);
        ShowNoGamePath = ShowEmpty && !_gamePath.HasValidPath;
        ShowNoRecords = ShowEmpty && !ShowNoGamePath;
        ShowContent = !ShowNoAccount && s is { IsEmpty: false };

        UpdateTabs(s);
        StoredSummary = string.Format(_localization["Pulls_StoredCount"], s?.RecordCount ?? 0);
        if (s is null)
        {
            // 没有账号（如当前账号刚被删除）：清掉总览里的头像与图表，不留上一个账号的数据。
            LatestRows.Clear();
            PityRows.Clear();
            ShareBars.Clear();
            DailyValues = [];
            DailyTips = [];
            DailyAxis = [];
            WinValues = [];
            FourStarValues = [];
            Timeline.Update(null, null);
            return;
        }

        CardPoolType? pool = CurrentPool;
        Timeline.Update(s, pool);
        NotifyViewMode();
        PullsSummary summary = pool is { } p ? PullsSummaryCalculator.ForPool(s, p) : PullsSummaryCalculator.ForAll(s);
        UpdateCards(summary, pool);
        if (pool is null) UpdateOverview(s);
    }

    private void UpdateTabs(PullsSnapshot? s)
    {
        Tabs[0].Title = _localization["Pulls_Overview"];
        Tabs[0].Count = s?.RecordCount ?? 0;
        for (int i = 0; i < PoolNames.MainPools.Count; i++)
        {
            CardPoolType pool = PoolNames.MainPools[i];
            Tabs[i + 1].Title = ShortTitle(pool);
            Tabs[i + 1].Count = s?.Pools[pool].PoolStatistics.Calculate.Tides ?? 0;
        }

        // 其他卡池：只列出有记录的（规范 02 §7.11）。
        var others = s is null
            ? []
            : s.Pools.Where(x => !PoolNames.IsMain(x.Key) && x.Value.PoolStatistics.Calculate.Tides > 0).Select(x => x.Key).ToList();
        CardPoolType? selectedOther = OtherPools.FirstOrDefault(x => x.IsSelected)?.Pool;
        OtherPools.Clear();
        foreach (CardPoolType pool in others)
        {
            OtherPools.Add(new PoolTab(pool, false, _localization[PoolNames.TitleKey(pool)])
            {
                Count = s!.Pools[pool].PoolStatistics.Calculate.Tides,
                IsSelected = pool == selectedOther,
            });
        }
        if (OtherPools.Count > 0 && !OtherPools.Any(x => x.IsSelected)) OtherPools[0].IsSelected = true;

        _otherTab.Title = _localization["Pulls_OtherPools"];
        _otherTab.Count = OtherPools.Sum(x => x.Count);
        bool showOther = OtherPools.Count > 0;
        if (showOther && !Tabs.Contains(_otherTab)) Tabs.Add(_otherTab);
        if (!showOther && Tabs.Contains(_otherTab))
        {
            Tabs.Remove(_otherTab);
            if (_otherTab.IsSelected) SelectTab(Tabs[0]);
        }
        if (_otherTab.IsSelected && OtherPools.FirstOrDefault(x => x.IsSelected)?.Pool is { } p) Records.Pool = p;
    }

    private void UpdateCards(PullsSummary summary, CardPoolType? pool)
    {
        TotalPulls = N(summary.TotalPulls);
        TotalPullsCaption = string.Empty;
        FiveStars = N(summary.FiveStars);
        FiveStarsCaption = string.Format(_localization["Pulls_LimitedPermanent"], summary.Limited, summary.Permanent);
        AveragePerFiveStar = summary.AveragePerFiveStar is { } avg ? avg.ToString("0.0", CultureInfo.InvariantCulture) : "—";
        AveragePerLimitedCaption = summary.AveragePerLimited is { } lim
            ? string.Format(_localization["Pulls_AvgPerLimited"], lim.ToString("0.0", CultureInfo.InvariantCulture))
            : string.Empty;
        WinRate = summary.WinRate is { } rate ? rate.ToString("0", CultureInfo.InvariantCulture) + "%" : "—";
        WinRateCaption = summary.WinRate is null
            ? _localization["Pulls_WinRateCharacterOnly"]
            : string.Format(_localization["Pulls_WinMiss"], summary.Wins, summary.Misses);
        Astrites = N(summary.Astrites);
        AstritesCaption = string.Empty;
        AveragePerUp = summary.AveragePerLimited is { } up ? up.ToString("0.0", CultureInfo.InvariantCulture) : "—";
        WinRatio = summary.WinRate is null ? string.Empty : $"{summary.Wins} / {summary.Wins + summary.Misses}";
    }

    private void UpdateOverview(PullsSnapshot s)
    {
        LatestRows.Clear();
        PityRows.Clear();
        foreach (CardPoolType pool in PoolNames.MainPools)
        {
            string title = _localization[PoolNames.TitleKey(pool)];
            FiveStarCard? latest = PoolRecordsViewModel.BuildCards(s.Pools[pool].PoolStatistics, pool).FirstOrDefault();
            string meta = latest is null ? title
                : string.Format(_localization["Pulls_PoolDate"], ShortTitle(pool), latest.Time?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "—");
            if (latest is not null) latest.SubtitleOverride = meta;
            LatestRows.Add(new LatestFiveStarRow(title, meta, latest));

            PoolProgress progress = PullsSummaryCalculator.Progress(s, pool);
            string next = progress.NextIsGuaranteed switch
            {
                true => _localization["Pulls_NextGuaranteed"],
                false => _localization["Pulls_NextSoftPity"],
                null => string.Empty,
            };
            PityRows.Add(new PityProgressRow(title, progress.CurrentPity,
                string.Format(_localization["Pulls_ToHardPityShort"], progress.PullsToHardPity), next));
        }

        // 近 30 天每日抽数；横轴标首、末与中间两个日期。
        DateTime today = DateTime.Today;
        var byDay = s.Insights.DailyPulls.ToDictionary(x => x.Date.Date, x => x.Pulls);
        var days = Enumerable.Range(0, DailyWindowDays).Select(i => today.AddDays(i - DailyWindowDays + 1)).ToArray();
        DailyValues = days.Select(d => (double)byDay.GetValueOrDefault(d)).ToArray();
        DailyTips = days.Select(d => string.Format(_localization["Pulls_PoolDate"], d.ToString("MM/dd", CultureInfo.InvariantCulture),
            byDay.GetValueOrDefault(d) + " " + _localization["Pulls_PullUnit"])).ToArray();
        DailyAxis = new[] { 0, 9, 19, DailyWindowDays - 1 }.Select(i => days[i].ToString("MM/dd", CultureInfo.InvariantCulture)).ToArray();
        DailyCaption = string.Format(_localization["Pulls_LastDays"], DailyWindowDays);

        int totalFive = PoolNames.MainPools.Sum(p => s.Pools[p].PoolStatistics.Calculate.HitGoldCount);
        ShareBars.Clear();
        foreach (CardPoolType pool in PoolNames.MainPools)
        {
            int five = s.Pools[pool].PoolStatistics.Calculate.HitGoldCount;
            ShareBars.Add(new PoolShareBar(ShortTitle(pool), five, totalFive == 0 ? 0 : five / (double)totalFive,
                string.Format(_localization["Pulls_FiveStarCount"], five)));
        }

        PullsSummary character = PullsSummaryCalculator.ForPool(s, CardPoolType.CharacterEvent);
        int decided = character.Wins + character.Misses;
        WinValues = [character.Wins, character.Misses];
        WinPercent = decided == 0 ? 0 : character.Wins * 100.0 / decided;
        WinLegend = string.Format(_localization["Pulls_WinCount"], character.Wins);
        LossLegend = string.Format(_localization["Pulls_LossCount"], character.Misses);

        var fourStars = s.AllRecords.Where(x => x.QualityLevel == 4).ToList();
        // 以资料中的类型为准；资料缺失时退回记录里的资源类型文字。
        int fourCharacters = fourStars.Count(x => _gameData.GetItemById(x.ResourceId)?.Type is { } type
            ? type == "Character"
            : x.ResourceType is "角色" or "Character" or "Resonators");
        double characterRatio = fourStars.Count == 0 ? 0 : fourCharacters / (double)fourStars.Count;
        FourStarValues = [fourCharacters, fourStars.Count - fourCharacters];
        FourStarPercent = characterRatio * 100;
        FourCharacterLegend = string.Format(_localization["Pulls_CharacterShare"], Percent(characterRatio));
        FourWeaponLegend = string.Format(_localization["Pulls_WeaponShare"], Percent(1 - characterRatio));
    }

    /// <summary>页签用短名：去掉卡池名末尾的“唤取”（英文为 “Convene”）。</summary>
    private string ShortTitle(CardPoolType pool)
    {
        string title = _localization[PoolNames.TitleKey(pool)];
        foreach (string suffix in (string[])["唤取", " Convene"])
        {
            if (title.EndsWith(suffix, StringComparison.Ordinal)) return title[..^suffix.Length];
        }
        return title;
    }

    private static string Percent(double ratio) => Math.Round(ratio * 100).ToString("0", CultureInfo.InvariantCulture) + "%";

    // —— 记录行：展开与右键菜单 ——

    /// <summary>视图里拼菜单文字用。</summary>
    public string Text(string key) => _localization[key];

    public void ReportExported(string name) =>
        _uiState.ShowToast(_localization["Toast_Success"], string.Format(_localization["Pulls_ExportSaved"], name), NotificationType.Success);

    public void ReportExportFailed(Exception ex) => _errors.Report(ex, _localization["Pulls_CtxExport"]);

    [RelayCommand]
    private static void ToggleExpand(FiveStarCard? card)
    {
        if (card is not null) card.IsExpanded = !card.IsExpanded;
    }

    [RelayCommand]
    private async Task CopyRecordAsync(FiveStarCard? card)
    {
        if (card is null) return;
        string kind = _localization[card.IsLimited == true ? "Pulls_Limited" : card.IsLimited == false ? "Pulls_Permanent" : "Pulls_FiveStar"];
        string text = string.Format(_localization["Pulls_RecordText"], card.Name, kind, card.TimeText, card.Pity);
        try
        {
            await _clipboard.SetTextAsync(text);
            _uiState.ShowToast(_localization["Toast_Success"], _localization["Pulls_CopiedRecord"], NotificationType.Success);
        }
        catch (Exception ex)
        {
            _errors.Report(ex, _localization["Pulls_CtxCopy"]);
        }
    }

    [RelayCommand]
    private void ViewRole(FiveStarCard? card) => _navigation.NavigateTo("roles", card?.ResourceId);

    [RelayCommand]
    private void LocateInCharts(FiveStarCard? card) => _navigation.NavigateTo(PullsChartsViewModel.PageKey, card?.Pool);

    private static string N(int value) => value.ToString("N0", CultureInfo.InvariantCulture);
}
