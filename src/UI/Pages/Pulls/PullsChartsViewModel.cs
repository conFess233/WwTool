using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WwTool.Core.Enums;
using WwTool.Core.Localization;
using WwTool.Core.Services.Gacha;
using WwTool.UI.Navigation;
using WwTool.UI.Shell;
using WwTool.UI.ViewModels;

namespace WwTool.UI.Pages.Pulls;

/// <summary>图表卡池筛选 Chip。<see cref="Pool"/> 为 null 表示全部卡池。</summary>
public sealed partial class ChartPoolOption(CardPoolType? pool, string title) : ObservableObject
{
    public CardPoolType? Pool { get; } = pool;
    public string AutomationId => Pool is null ? "charts-pool-all" : "charts-pool-" + Pool;

    [ObservableProperty]
    private string _title = title;

    [ObservableProperty]
    private bool _isSelected;
}

/// <summary>“各卡池抽数占比”的图例行；<see cref="Index"/> 决定色块（与环形图各段同色）。</summary>
public sealed record ShareLegendRow(int Index, string Title, string Text)
{
    public bool IsFirst => Index == 0;
    public bool IsSecond => Index == 1;
    public bool IsThird => Index == 2;
    public bool IsFourth => Index >= 3;
}

/// <summary>“每个五星花费抽数”的一行。</summary>
public sealed record FiveStarCostRow(string Name, int Pity, double Ratio, bool IsHardPity, bool? IsLimited)
{
    public bool IsLimitedTag => IsLimited == true;
}

/// <summary>稀有度构成与不歪率的一行；<see cref="Kind"/> 决定刻度条颜色（gold / four / muted）。</summary>
public sealed record RarityRow(string Title, string Text, double Ratio, string Kind = "gold")
{
    public bool IsFour => Kind == "four";
    public bool IsMuted => Kind == "muted";
}

/// <summary>
/// 抽卡统计的“详细图表”子页面（定稿 FinalCharts）：六张图表，数据由 Core 的 <see cref="PullsChartBuilder"/> 生成，
/// 这里只负责筛选条件与把数据转换为显示用的数值与文字。离开页面期间不重建，回来时再算。
/// 悬浮按钮与抽卡统计页相同（扩展态「获取记录」）。
/// </summary>
public sealed partial class PullsChartsViewModel : ObservableObject, INavigationAware, IFabProvider
{
    public const string PageKey = "pulls-charts";

    /// <summary>每日活跃显示的天数。</summary>
    private const int DailyWindowDays = 90;

    /// <summary>“每个五星花费抽数”最多显示的条数。</summary>
    private const int MaxCostRows = 8;

    private readonly PullsSession _session;
    private readonly INavigationService _navigation;
    private readonly LocalizationService _localization;
    private readonly PullsOverviewViewModel _pulls;
    private bool _isActive;
    private bool _dirty = true;

    public PullsChartsViewModel(
        PullsSession session,
        INavigationService navigation,
        LocalizationService localization,
        PullsOverviewViewModel pulls)
    {
        _session = session;
        _navigation = navigation;
        _localization = localization;
        _pulls = pulls;

        PoolOptions.Add(new ChartPoolOption(null, string.Empty) { IsSelected = true });
        foreach (CardPoolType pool in PoolNames.MainPools) PoolOptions.Add(new ChartPoolOption(pool, string.Empty));
        TimeRanges =
        [
            new LocalizedOption(6, "Pulls_Filter6Months", localization),
            new LocalizedOption(3, "Stat_Filter_Date3Months", localization),
            new LocalizedOption(1, "Stat_Filter_Date1Month", localization),
            new LocalizedOption(0, "Stat_Filter_DateAll", localization),
        ];
        _selectedTimeRange = TimeRanges[0];

        _session.SnapshotReplaced += (_, _) => Invalidate();
        _localization.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == LocalizationService.IndexerName) Invalidate();
        };
        pulls.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(PullsOverviewViewModel.IsFabBusy)) OnPropertyChanged(nameof(IsFabBusy));
        };
        UpdatePoolTitles();
    }

    public PullsSession Session => _session;

    public IReadOnlyList<FabAction> FabActions => _pulls.FabActions;
    public FabTrigger? FabTrigger => _pulls.FabTrigger;
    public bool IsFabBusy => _pulls.IsFabBusy;

    public ObservableCollection<ChartPoolOption> PoolOptions { get; } = [];

    public IReadOnlyList<LocalizedOption> TimeRanges { get; }

    [ObservableProperty]
    private LocalizedOption _selectedTimeRange;

    [ObservableProperty] private bool _isEmpty = true;
    [ObservableProperty] private string _basis = string.Empty;

    [ObservableProperty] private IReadOnlyList<double> _dailyValues = [];
    [ObservableProperty] private IReadOnlyList<string> _dailyTips = [];
    [ObservableProperty] private IReadOnlyList<string> _dailyAxis = ["", "", "", ""];
    [ObservableProperty] private string _dailyCaption = string.Empty;

    [ObservableProperty] private IReadOnlyList<double> _pityValues = [];
    [ObservableProperty] private string _pityCaption = string.Empty;
    public IReadOnlyList<string> PityLabels => PullsChartData.PityLabels;

    [ObservableProperty] private IReadOnlyList<double> _shareValues = [];
    [ObservableProperty] private double _shareTotal;
    [ObservableProperty] private IReadOnlyList<ShareLegendRow> _shareLegend = [];
    [ObservableProperty] private string _shareCaption = string.Empty;

    [ObservableProperty] private IReadOnlyList<FiveStarCostRow> _costRows = [];

    [ObservableProperty] private IReadOnlyList<double> _cumulativeValues = [];
    [ObservableProperty] private IReadOnlyList<string> _cumulativeLabels = [];
    [ObservableProperty] private string _cumulativeCaption = string.Empty;

    [ObservableProperty] private IReadOnlyList<RarityRow> _rarityRows = [];
    [ObservableProperty] private IReadOnlyList<RarityRow> _winRows = [];

    public void OnNavigatedTo(object? parameter)
    {
        _isActive = true;
        _ = _session.EnsureLoadedAsync();
        // 从记录行右键「在详细图表中定位」进入时，选中该卡池。
        if (parameter is CardPoolType pool && PoolOptions.FirstOrDefault(o => o.Pool == pool) is { } option)
        {
            foreach (ChartPoolOption o in PoolOptions) o.IsSelected = ReferenceEquals(o, option);
            _dirty = true;
        }
        if (_dirty) Rebuild();
    }

    public void OnNavigatedFrom() => _isActive = false;

    [RelayCommand]
    private void Back() => _navigation.NavigateTo("pulls");

    [RelayCommand]
    private void SelectPool(ChartPoolOption option)
    {
        foreach (ChartPoolOption o in PoolOptions) o.IsSelected = ReferenceEquals(o, option);
        Invalidate();
    }

    partial void OnSelectedTimeRangeChanged(LocalizedOption value) => Invalidate();

    private void Invalidate()
    {
        _dirty = true;
        if (_isActive) Rebuild();
    }

    private void ClearCharts()
    {
        IsEmpty = true;
        DailyValues = [];
        DailyTips = [];
        DailyAxis = ["", "", "", ""];
        DailyCaption = string.Empty;
        PityValues = [];
        PityCaption = string.Empty;
        ShareValues = [];
        ShareTotal = 0;
        ShareLegend = [];
        ShareCaption = string.Empty;
        CostRows = [];
        CumulativeValues = [];
        CumulativeLabels = [];
        CumulativeCaption = string.Empty;
        RarityRows = [];
        WinRows = [];
    }

    private void Rebuild()
    {
        _dirty = false;
        UpdatePoolTitles();
        PullsSnapshot? snapshot = _session.Snapshot;
        Basis = string.Format(_localization["Pulls_ChartsBasis"], snapshot?.RecordCount ?? 0);
        if (snapshot is null)
        {
            // 没有账号（如当前账号刚被删除）：清空全部图表，不留上一个账号的数据与头像。
            ClearCharts();
            return;
        }

        CardPoolType? pool = PoolOptions.First(o => o.IsSelected).Pool;
        int months = SelectedTimeRange.Value is int m ? m : 0;
        var filter = new PullsChartFilter(
            pool is { } p ? new HashSet<CardPoolType> { p } : null,
            months > 0 ? DateTime.Today.AddMonths(-months) : null);
        PullsChartData data = PullsChartBuilder.Build(snapshot, filter);
        IsEmpty = data.IsEmpty;
        string unit = _localization["Pulls_PullUnit"];

        // 每日抽卡活跃：近 90 天，横轴标首、末与两个中间日期。
        DateTime today = DateTime.Today;
        var byDay = data.Daily.ToDictionary(x => x.Date.Date, x => x.Pulls);
        var days = Enumerable.Range(0, DailyWindowDays).Select(i => today.AddDays(i - DailyWindowDays + 1)).ToArray();
        DailyValues = days.Select(d => (double)byDay.GetValueOrDefault(d)).ToArray();
        DailyTips = days.Select(d => string.Format(_localization["Pulls_PoolDate"], d.ToString("MM/dd", CultureInfo.InvariantCulture),
            byDay.GetValueOrDefault(d) + " " + unit)).ToArray();
        DailyAxis = new[] { 0, 30, 60, DailyWindowDays - 1 }.Select(i => days[i].ToString("MM/dd", CultureInfo.InvariantCulture)).ToArray();
        DailyCaption = string.Format(_localization["Pulls_LastDays"], DailyWindowDays);

        // 五星出货抽数分布（8 档）
        PityValues = data.PityBins.Select(x => (double)x).ToArray();
        PityCaption = data.AveragePity is { } avg
            ? string.Format(_localization["Pulls_AveragePityCaption"], avg.ToString("0.0", CultureInfo.InvariantCulture))
            : string.Empty;

        // 各卡池抽数占比（按主卡池顺序，颜色依次为 Accent、Ink、Muted、Track）
        var legend = new List<ShareLegendRow>();
        var shareValues = new List<double>();
        void AddShare(string title, int pulls)
        {
            double ratio = data.TotalPulls == 0 ? 0 : pulls / (double)data.TotalPulls;
            legend.Add(new ShareLegendRow(legend.Count, title,
                string.Format(_localization["Pulls_CountShare"], pulls.ToString("N0", CultureInfo.InvariantCulture),
                    ratio.ToString("0.0%", CultureInfo.InvariantCulture))));
            shareValues.Add(pulls);
        }
        foreach (PoolShare share in data.PoolShares.Where(x => PoolNames.IsMain(x.Pool)).OrderBy(x => PoolOrder(x.Pool)))
        {
            AddShare(ShortTitle(share.Pool), share.Pulls);
        }
        // 其他卡池（联动等）合并为一项。
        int others = data.PoolShares.Where(x => !PoolNames.IsMain(x.Pool)).Sum(x => x.Pulls);
        if (others > 0) AddShare(_localization["Pulls_OtherPools"], others);
        ShareValues = shareValues;
        ShareTotal = data.TotalPulls;
        ShareLegend = legend;
        ShareCaption = string.Format(_localization["Pulls_TotalPullsCaption"], data.TotalPulls.ToString("N0", CultureInfo.InvariantCulture));

        // 每个五星花费抽数（从近到远）
        CostRows = data.FiveStars.Take(MaxCostRows)
            .Select(f => new FiveStarCostRow(f.Name, f.Pity, f.Pity / (double)PullsSummaryCalculator.HardPity,
                f.Pity >= PullsSummaryCalculator.HardPity, f.IsLimited))
            .ToList();

        // 累计抽数：按月取月末累计值，最多显示最近 12 个月。
        var monthly = data.Cumulative
            .GroupBy(x => new DateTime(x.Date.Year, x.Date.Month, 1))
            .Select(g => (Month: g.Key, Pulls: g.Last().Pulls))
            .OrderBy(x => x.Month)
            .TakeLast(12)
            .ToList();
        CumulativeValues = monthly.Select(x => (double)x.Pulls).ToArray();
        CumulativeLabels = monthly.Select(x => x.Month.ToString("MM", CultureInfo.InvariantCulture)).ToArray();
        CumulativeCaption = string.Format(_localization["Pulls_CumulativeCaption"], data.Astrites.ToString("N0", CultureInfo.InvariantCulture));

        // 稀有度构成与小保底不歪率
        double total = Math.Max(1, data.TotalPulls);
        RarityRow Rarity(string key, int count, string kind) =>
            new(_localization[key], string.Format(_localization["Pulls_CountShare"], count.ToString("N0", CultureInfo.InvariantCulture),
                (count / total).ToString("0.0%", CultureInfo.InvariantCulture)), count / total, kind);
        RarityRows =
        [
            Rarity("Pulls_FiveStar", data.FiveStarCount, "gold"),
            Rarity("Pulls_FourStar", data.FourStarCount, "four"),
            Rarity("Pulls_ThreeStar", data.ThreeStarCount, "muted"),
        ];
        WinRows =
        [
            WinRow("Pulls_CharacterWinRate", PullsSummaryCalculator.ForPool(snapshot, CardPoolType.CharacterEvent)),
            WinRow("Pulls_WeaponWinRate", PullsSummaryCalculator.ForPool(snapshot, CardPoolType.WeaponEvent)),
        ];
    }

    private RarityRow WinRow(string key, PullsSummary summary)
    {
        int decided = summary.Wins + summary.Misses;
        double ratio = decided == 0 ? 0 : summary.Wins / (double)decided;
        string text = decided == 0 ? "—"
            : string.Format(_localization["Pulls_WinFraction"], summary.Wins, decided, ratio.ToString("0%", CultureInfo.InvariantCulture));
        return new RarityRow(_localization[key], text, ratio);
    }

    private static int PoolOrder(CardPoolType pool)
    {
        int index = PoolNames.MainPools.ToList().IndexOf(pool);
        return index < 0 ? int.MaxValue : index;
    }

    private string ShortTitle(CardPoolType pool)
    {
        string title = _localization[PoolNames.TitleKey(pool)];
        foreach (string suffix in (string[])["唤取", " Convene"])
        {
            if (title.EndsWith(suffix, StringComparison.Ordinal)) return title[..^suffix.Length];
        }
        return title;
    }

    private void UpdatePoolTitles()
    {
        PoolOptions[0].Title = _localization["Pulls_AllPools"];
        for (int i = 1; i < PoolOptions.Count; i++) PoolOptions[i].Title = ShortTitle(PoolOptions[i].Pool!.Value);
    }
}
