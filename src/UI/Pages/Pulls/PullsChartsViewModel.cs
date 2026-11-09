using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;
using WwTool.Core.Abstractions;
using WwTool.Core.Enums;
using WwTool.Core.Localization;
using WwTool.Core.Models.Config;
using WwTool.Core.Services.Gacha;
using WwTool.UI.Navigation;
using WwTool.UI.Services;
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

/// <summary>“各卡池抽数占比”的图例行（色块与饼图扇区同色）。</summary>
public sealed record ShareLegendRow(string Title, string Text, Avalonia.Media.IBrush Swatch);

/// <summary>“每个五星花费抽数”的一行。</summary>
public sealed record FiveStarCostRow(string Name, int Pity, double Ratio, bool IsHardPity, bool? IsLimited)
{
    public bool IsLimitedTag => IsLimited == true;
}

/// <summary>稀有度构成的一行。</summary>
public sealed record RarityRow(string Title, string Text, double Ratio);

/// <summary>
/// 抽卡统计的“详细图表”子页面。数据由 Core 的 <see cref="PullsChartBuilder"/> 生成，
/// 这里只负责筛选条件与把数据转换为 LiveCharts 序列（颜色取自主题）。离开页面期间不重建，回来时再算。
/// </summary>
public sealed partial class PullsChartsViewModel : ObservableObject, INavigationAware
{
    public const string PageKey = "pulls-charts";

    /// <summary>超过此天数时每日活跃改为按周合计。</summary>
    private const int WeeklyThresholdDays = 120;

    /// <summary>“每个五星花费抽数”最多显示的条数。</summary>
    private const int MaxCostRows = 30;

    private readonly PullsSession _session;
    private readonly INavigationService _navigation;
    private readonly LocalizationService _localization;
    private readonly IConfigService _config;
    private bool _isActive;
    private bool _dirty = true;

    public PullsChartsViewModel(
        PullsSession session,
        INavigationService navigation,
        LocalizationService localization,
        ThemeService theme,
        IConfigService config)
    {
        _session = session;
        _navigation = navigation;
        _localization = localization;
        _config = config;

        PoolOptions.Add(new ChartPoolOption(null, string.Empty) { IsSelected = true });
        foreach (CardPoolType pool in PoolNames.MainPools) PoolOptions.Add(new ChartPoolOption(pool, string.Empty));
        TimeRanges =
        [
            new LocalizedOption(0, "Stat_Filter_DateAll", localization),
            new LocalizedOption(1, "Stat_Filter_Date1Month", localization),
            new LocalizedOption(3, "Stat_Filter_Date3Months", localization),
            new LocalizedOption(6, "Pulls_Filter6Months", localization),
        ];
        _selectedTimeRange = TimeRanges[0];

        _session.SnapshotReplaced += (_, _) => Invalidate();
        theme.ThemeChanged += (_, _) => Invalidate();
        _localization.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == LocalizationService.IndexerName) Invalidate();
        };
        _config.User.PropertyChanged += OnUserConfigChanged;
        UpdatePoolTitles();
    }

    public PullsSession Session => _session;

    public ObservableCollection<ChartPoolOption> PoolOptions { get; } = [];

    public IReadOnlyList<LocalizedOption> TimeRanges { get; }

    [ObservableProperty]
    private LocalizedOption _selectedTimeRange;

    /// <summary>减少动画时缩短图表动画。</summary>
    public TimeSpan AnimationsSpeed => _config.User.IsReducedMotionEnabled ? TimeSpan.FromMilliseconds(80) : TimeSpan.FromMilliseconds(220);

    [ObservableProperty] private bool _isEmpty = true;
    [ObservableProperty] private string _basis = string.Empty;

    [ObservableProperty] private ISeries[] _dailySeries = [];
    [ObservableProperty] private Axis[] _dailyXAxes = [];
    [ObservableProperty] private Axis[] _dailyYAxes = [];
    [ObservableProperty] private string _dailyCaption = string.Empty;

    [ObservableProperty] private ISeries[] _pitySeries = [];
    [ObservableProperty] private Axis[] _pityXAxes = [];
    [ObservableProperty] private Axis[] _pityYAxes = [];
    [ObservableProperty] private string _pityCaption = string.Empty;

    [ObservableProperty] private ISeries[] _shareSeries = [];
    [ObservableProperty] private IReadOnlyList<ShareLegendRow> _shareLegend = [];
    [ObservableProperty] private string _shareCaption = string.Empty;

    [ObservableProperty] private IReadOnlyList<FiveStarCostRow> _costRows = [];

    [ObservableProperty] private ISeries[] _cumulativeSeries = [];
    [ObservableProperty] private Axis[] _cumulativeXAxes = [];
    [ObservableProperty] private Axis[] _cumulativeYAxes = [];
    [ObservableProperty] private string _cumulativeCaption = string.Empty;

    [ObservableProperty] private IReadOnlyList<RarityRow> _rarityRows = [];
    [ObservableProperty] private string _winText = "—";
    [ObservableProperty] private double _winRatio;

    public void OnNavigatedTo(object? parameter)
    {
        _isActive = true;
        _ = _session.EnsureLoadedAsync();
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

    private void OnUserConfigChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(UserConfig.IsReducedMotionEnabled)) OnPropertyChanged(nameof(AnimationsSpeed));
    }

    private void Invalidate()
    {
        _dirty = true;
        if (_isActive) Rebuild();
    }

    private void Rebuild()
    {
        _dirty = false;
        UpdatePoolTitles();
        PullsSnapshot? snapshot = _session.Snapshot;
        string region = _localization[_session.Region == GachaServerRegion.China ? "Stat_GachaServerCN" : "Stat_GachaServerInternational"];
        Basis = string.Format(_localization["Pulls_ChartsBasis"], snapshot?.RecordCount ?? 0, region);
        if (snapshot is null)
        {
            IsEmpty = true;
            return;
        }

        CardPoolType? pool = PoolOptions.First(o => o.IsSelected).Pool;
        int months = SelectedTimeRange.Value is int m ? m : 0;
        var filter = new PullsChartFilter(
            pool is { } p ? new HashSet<CardPoolType> { p } : null,
            months > 0 ? DateTime.Today.AddMonths(-months) : null);
        PullsChartData data = PullsChartBuilder.Build(snapshot, filter);
        IsEmpty = data.IsEmpty;

        ChartPalette palette = ChartPalette.FromResources();
        var text = new SolidColorPaint(palette.Text);
        var grid = new SolidColorPaint(palette.Grid) { StrokeThickness = 1 };
        const float textSize = 12;

        // 每日活跃；范围超过 120 天时按周合计（规范 02 §8：大数据量时聚合），避免柱子细到看不清。
        IReadOnlyList<DailyPulls> buckets = data.Daily;
        bool weekly = data.Daily.Count > WeeklyThresholdDays;
        if (weekly)
        {
            buckets = data.Daily
                .GroupBy(x => x.Date.AddDays(-(((int)x.Date.DayOfWeek + 6) % 7)))
                .Select(g => new DailyPulls(g.Key, g.Sum(x => x.Pulls)))
                .ToList();
        }
        DailySeries =
        [
            new ColumnSeries<int>
            {
                Values = buckets.Select(x => x.Pulls).ToArray(),
                Fill = new SolidColorPaint(palette.Primary),
                MaxBarWidth = 14,
                Rx = 3,
                Ry = 3,
                Padding = 1,
                Name = _localization["Stat_DailyPulls"],
            },
        ];
        DailyXAxes = [Axis(buckets.Select(x => x.Date.ToString("MM/dd", CultureInfo.InvariantCulture)).ToArray(), text, null, textSize)];
        DailyYAxes = [Axis(null, text, grid, textSize, minLimit: 0)];
        DailyCaption = weekly
            ? string.Format(_localization["Pulls_WeeklyCaption"], buckets.Count, buckets.Max(x => x.Pulls))
            : string.Format(_localization["Pulls_DailyCaption"], data.Daily.Count, data.DailyPeak);

        // 五星抽数分布
        PitySeries =
        [
            new ColumnSeries<int>
            {
                Values = data.PityBins.ToArray(),
                Fill = new SolidColorPaint(palette.Primary),
                MaxBarWidth = 36,
                Rx = 4,
                Ry = 4,
                DataLabelsPaint = text,
                DataLabelsSize = textSize,
                DataLabelsPosition = LiveChartsCore.Measure.DataLabelsPosition.Top,
                Name = _localization["Pulls_FiveStar"],
            },
        ];
        PityXAxes = [Axis(PullsChartData.PityLabels.ToArray(), text, null, textSize)];
        PityYAxes = [Axis(null, text, grid, textSize, minLimit: 0)];
        PityCaption = data.AveragePity is { } avg
            ? string.Format(_localization["Pulls_AveragePityCaption"], avg.ToString("0.0", CultureInfo.InvariantCulture))
            : string.Empty;

        // 各卡池抽数占比
        var shares = new List<ISeries>();
        var legend = new List<ShareLegendRow>();
        for (int i = 0; i < data.PoolShares.Count; i++)
        {
            PoolShare share = data.PoolShares[i];
            SKColor color = PoolColor(palette, share.Pool);
            string title = _localization[PoolNames.TitleKey(share.Pool)];
            shares.Add(new PieSeries<int> { Values = [share.Pulls], InnerRadius = 46, Fill = new SolidColorPaint(color), Name = title, HoverPushout = 4 });
            double ratio = data.TotalPulls == 0 ? 0 : share.Pulls / (double)data.TotalPulls;
            legend.Add(new ShareLegendRow(title, $"{share.Pulls:N0} · {ratio:P1}",
                new Avalonia.Media.SolidColorBrush(new Avalonia.Media.Color(color.Alpha, color.Red, color.Green, color.Blue))));
        }
        ShareSeries = [.. shares];
        ShareLegend = legend;
        ShareCaption = string.Format(_localization["Pulls_TotalPullsCaption"], data.TotalPulls.ToString("N0", CultureInfo.InvariantCulture));

        // 每个五星花费抽数（新→旧）
        CostRows = data.FiveStars.Take(MaxCostRows)
            .Select(f => new FiveStarCostRow(f.Name, f.Pity, f.Pity / (double)PullsSummaryCalculator.HardPity,
                f.Pity >= PullsSummaryCalculator.HardPity, f.IsLimited))
            .ToList();

        // 累计抽数
        CumulativeSeries =
        [
            new LineSeries<int>
            {
                Values = data.Cumulative.Select(x => x.Pulls).ToArray(),
                Stroke = new SolidColorPaint(palette.Primary) { StrokeThickness = 2 },
                Fill = new SolidColorPaint(palette.Primary.WithAlpha(0x30)),
                GeometrySize = 0,
                LineSmoothness = 0.3,
                Name = _localization["Stat_CumulativePulls"],
            },
        ];
        CumulativeXAxes = [Axis(data.Cumulative.Select(x => x.Date.ToString("yy/MM/dd", CultureInfo.InvariantCulture)).ToArray(), text, null, textSize)];
        CumulativeYAxes = [Axis(null, text, grid, textSize, minLimit: 0)];
        CumulativeCaption = string.Format(_localization["Pulls_CumulativeCaption"], data.Astrites.ToString("N0", CultureInfo.InvariantCulture));

        // 稀有度构成与不歪率
        double total = Math.Max(1, data.TotalPulls);
        RarityRows =
        [
            new RarityRow(_localization["Pulls_FiveStar"], $"{data.FiveStarCount:N0} · {data.FiveStarCount / total:P1}", data.FiveStarCount / total),
            new RarityRow(_localization["Pulls_FourStar"], $"{data.FourStarCount:N0} · {data.FourStarCount / total:P1}", data.FourStarCount / total),
            new RarityRow(_localization["Pulls_ThreeStar"], $"{data.ThreeStarCount:N0} · {data.ThreeStarCount / total:P1}", data.ThreeStarCount / total),
        ];
        WinRatio = data.WinRate is { } rate ? rate / 100 : 0;
        WinText = data.WinRate is { } r
            ? string.Format(_localization["Pulls_WinFraction"], data.Wins, data.Wins + data.Misses, r.ToString("0", CultureInfo.InvariantCulture) + "%")
            : "—";
    }

    private void UpdatePoolTitles()
    {
        PoolOptions[0].Title = _localization["Pulls_AllPools"];
        for (int i = 1; i < PoolOptions.Count; i++)
        {
            string title = _localization[PoolNames.TitleKey(PoolOptions[i].Pool!.Value)];
            foreach (string suffix in (string[])["唤取", " Convene"])
            {
                if (title.EndsWith(suffix, StringComparison.Ordinal)) title = title[..^suffix.Length];
            }
            PoolOptions[i].Title = title;
        }
    }

    private static SKColor PoolColor(ChartPalette palette, CardPoolType pool) => pool switch
    {
        CardPoolType.CharacterEvent => palette.Primary,
        CardPoolType.WeaponEvent => palette.Secondary,
        CardPoolType.CharacterStandard => palette.Tertiary,
        CardPoolType.WeaponStandard => palette.FourStar,
        _ => palette.Text,
    };

    private static Axis Axis(string[]? labels, SolidColorPaint text, SolidColorPaint? separators, float textSize, double? minLimit = null) => new()
    {
        Labels = labels,
        LabelsPaint = text,
        SeparatorsPaint = separators,
        TextSize = textSize,
        MinLimit = minLimit,
        ShowSeparatorLines = separators is not null,
    };
}
