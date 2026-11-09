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

/// <summary>总览页“各卡池最近五星”与“未出金进度”的一行。</summary>
public sealed record PoolOverviewRow(
    CardPoolType Pool,
    string PoolTitle,
    PoolProgress Progress,
    string? LatestName,
    string LatestMeta,
    int LatestPity,
    bool? LatestIsLimited,
    string NextKindText)
{
    public bool HasLatest => LatestName is not null;
    public bool LatestLimitedTag => LatestIsLimited == true;
    public bool LatestPermanentTag => LatestIsLimited == false;
    public string PityText => Progress.CurrentPity.ToString(CultureInfo.InvariantCulture);
    public bool HasNextKind => NextKindText.Length > 0;
}

/// <summary>每日活跃柱（近 30 天）。</summary>
public sealed record DailyBar(DateTime Date, int Pulls, double Height, string Tip);

/// <summary>各卡池五星分布条。</summary>
public sealed record PoolShareBar(string PoolTitle, int FiveStars, double Ratio, string Text);

/// <summary>
/// 抽卡统计页：卡池页签、五张统计卡、总览内容，选中具体卡池时显示 <see cref="Records"/>。
/// 数据来自 <see cref="PullsSession"/>；本类只把快照转换为显示用的文字与列表。
/// </summary>
public sealed partial class PullsOverviewViewModel : ObservableObject, INavigationAware, IFabProvider
{
    private const int DailyWindowDays = 30;
    private const double DailyBarMaxHeight = 64;

    private readonly PullsSession _session;
    private readonly LocalizationService _localization;
    private readonly INavigationService _navigation;
    private readonly GameDataService _gameData;

    public PullsOverviewViewModel(
        PullsSession session,
        GameDataService gameData,
        PoolRecordsViewModel records,
        AccountContext accounts,
        LocalizationService localization,
        INavigationService navigation)
    {
        _session = session;
        _localization = localization;
        _navigation = navigation;
        _gameData = gameData;
        Records = records;
        Accounts = accounts;
        Session = session;

        const string icons = "avares://WwTool/Assets/Images/";
        FabActions =
        [
            new FabAction("cloud", "Stat_GetCloudData", icons + "Cloud.png", new AsyncRelayCommand(_session.SyncAsync), localization),
            new FabAction("load-local", "Stat_LoadLocalData", icons + "Load.png", new AsyncRelayCommand(() => _session.ReloadAsync(showMessage: true)), localization),
            new FabAction("import-url", "Stat_AutoImportUrl", icons + "Import.png", new AsyncRelayCommand(_session.AutoImportUrlAsync), localization),
            new FabAction("clear", "Stat_ClearData", icons + "Trash.png", new RelayCommand(_session.ClearDisplay), localization,
                isDanger: true, confirmMessageKey: "Msg_ConfirmClearData"),
        ];

        Tabs.Add(new PoolTab(null, false, string.Empty));
        foreach (CardPoolType pool in PoolNames.MainPools) Tabs.Add(new PoolTab(pool, false, string.Empty));
        _otherTab = new PoolTab(null, true, string.Empty);
        Tabs[0].IsSelected = true;

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

    public AccountContext Accounts { get; }

    public PoolRecordsViewModel Records { get; }

    public IReadOnlyList<FabAction> FabActions { get; }

    /// <summary>总览 + 4 个主卡池 + （有记录时）其他卡池。</summary>
    public ObservableCollection<PoolTab> Tabs { get; } = [];

    private readonly PoolTab _otherTab;

    /// <summary>“其他卡池”页签下可选的卡池（5–13 中有记录的）。</summary>
    public ObservableCollection<PoolTab> OtherPools { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsOverview), nameof(IsPoolView), nameof(IsOtherSelected))]
    private PoolTab? _selectedTab;

    public bool IsOverview => SelectedTab?.Pool is null && SelectedTab?.IsOther != true;
    public bool IsPoolView => !IsOverview;
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
    [ObservableProperty] private string _astrites = "0";
    [ObservableProperty] private string _astritesCaption = string.Empty;

    // —— 总览内容 ——
    public ObservableCollection<PoolOverviewRow> PoolRows { get; } = [];
    public ObservableCollection<DailyBar> DailyBars { get; } = [];
    public ObservableCollection<PoolShareBar> ShareBars { get; } = [];
    [ObservableProperty] private string _dailyCaption = string.Empty;
    [ObservableProperty] private string _winLossText = string.Empty;
    [ObservableProperty] private double _winRatio;
    [ObservableProperty] private string _fourStarSplitText = string.Empty;
    [ObservableProperty] private double _fourStarCharacterRatio;

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
    }

    /// <summary>当前页签对应的卡池；总览为 null。</summary>
    private CardPoolType? CurrentPool => SelectedTab is { IsOther: true } ? OtherPools.FirstOrDefault(x => x.IsSelected)?.Pool : SelectedTab?.Pool;

    private void Refresh()
    {
        SelectedTab ??= Tabs[0];
        PullsSnapshot? s = _session.Snapshot;
        ShowNoAccount = _session.Uid is null;
        ShowEmpty = !ShowNoAccount && !_session.IsLoading && (s is null || s.IsEmpty);
        ShowContent = !ShowNoAccount && s is { IsEmpty: false };

        UpdateTabs(s);
        string region = _localization[_session.Region == GachaServerRegion.China ? "Stat_GachaServerCN" : "Stat_GachaServerInternational"];
        StoredSummary = string.Format(_localization["Pulls_StoredSummary"], s?.RecordCount ?? 0, region);
        if (s is null) return;

        CardPoolType? pool = CurrentPool;
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
        TotalPullsCaption = pool is { } p ? _localization[PoolNames.TitleKey(p)] : _localization["Pulls_AllPools"];
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
        AstritesCaption = string.Format(_localization["Pulls_AstritesFormula"], N(summary.TotalPulls));
    }

    private void UpdateOverview(PullsSnapshot s)
    {
        PoolRows.Clear();
        foreach (CardPoolType pool in PoolNames.MainPools)
        {
            PoolProgress progress = PullsSummaryCalculator.Progress(s, pool);
            var latest = progress.LatestFiveStar;
            string meta = latest is null ? string.Empty
                : $"{(DateTime.TryParse(latest.GachaData.Time, CultureInfo.InvariantCulture, DateTimeStyles.None, out var t) ? t.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) : latest.GachaData.Time)} · {ShortTitle(pool)}";
            string next = progress.NextIsGuaranteed switch
            {
                true => _localization["Pulls_NextGuaranteed"],
                false => _localization["Pulls_NextSoftPity"],
                null => string.Empty,
            };
            PoolRows.Add(new PoolOverviewRow(pool, _localization[PoolNames.TitleKey(pool)], progress,
                latest?.GachaData.Name, meta, latest?.Pity ?? 0, latest?.IsMiss is null ? null : !latest.IsMiss.Value, next));
        }

        // 近 30 天每日抽数。
        DateTime today = DateTime.Today;
        var byDay = s.Insights.DailyPulls.ToDictionary(x => x.Date.Date, x => x.Pulls);
        int max = Enumerable.Range(0, DailyWindowDays).Select(i => byDay.GetValueOrDefault(today.AddDays(i - DailyWindowDays + 1))).DefaultIfEmpty().Max();
        DailyBars.Clear();
        for (int i = 0; i < DailyWindowDays; i++)
        {
            DateTime day = today.AddDays(i - DailyWindowDays + 1);
            int pulls = byDay.GetValueOrDefault(day);
            double height = max == 0 ? 2 : Math.Max(2, pulls / (double)max * DailyBarMaxHeight);
            DailyBars.Add(new DailyBar(day, pulls, height, $"{day:MM/dd} · {pulls}"));
        }
        DailyCaption = string.Format(_localization["Pulls_DailyCaption"], DailyWindowDays, max);

        int totalFive = PoolNames.MainPools.Sum(p => s.Pools[p].PoolStatistics.Calculate.HitGoldCount);
        ShareBars.Clear();
        foreach (CardPoolType pool in PoolNames.MainPools)
        {
            int five = s.Pools[pool].PoolStatistics.Calculate.HitGoldCount;
            ShareBars.Add(new PoolShareBar(ShortTitle(pool), five, totalFive == 0 ? 0 : five / (double)totalFive, five.ToString(CultureInfo.InvariantCulture)));
        }

        PullsSummary character = PullsSummaryCalculator.ForPool(s, CardPoolType.CharacterEvent);
        WinRatio = character.Wins + character.Misses == 0 ? 0 : character.Wins / (double)(character.Wins + character.Misses);
        WinLossText = string.Format(_localization["Pulls_WinMiss"], character.Wins, character.Misses);

        var fourStars = s.AllRecords.Where(x => x.QualityLevel == 4).ToList();
        // 以资料中的类型为准；资料缺失时退回记录里的资源类型文字。
        int fourCharacters = fourStars.Count(x => _gameData.GetItemById(x.ResourceId)?.Type is { } type
            ? type == "Character"
            : x.ResourceType is "角色" or "Character" or "Resonators");
        FourStarCharacterRatio = fourStars.Count == 0 ? 0 : fourCharacters / (double)fourStars.Count;
        FourStarSplitText = fourStars.Count == 0 ? "—"
            : $"{_localization["Pulls_FourStarCharacter"]} {FourStarCharacterRatio:P0} · {_localization["Pulls_FourStarWeapon"]} {1 - FourStarCharacterRatio:P0}";
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

    private static string N(int value) => value.ToString("N0", CultureInfo.InvariantCulture);
}
