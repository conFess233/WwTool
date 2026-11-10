using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WwTool.Core.Enums;
using WwTool.Core.Localization;
using WwTool.Core.Models.Statistics;
using WwTool.Core.Services.Gacha;
using WwTool.UI.ViewModels;

namespace WwTool.UI.Pages.Pulls;

/// <summary>五星卡片上的保底类型标签。</summary>
public enum PityKind
{
    /// <summary>常驻池，无小 / 大保底之分。</summary>
    Standard,
    SoftPity,
    Guaranteed,
}

/// <summary>期间获得的四星：名称 × 次数。</summary>
public sealed record FourStarChip(int ResourceId, string Name, int Count)
{
    public string Text => $"{Name} x{Count}";
    public string CountText => $"x{Count}";
    public string IconPath => $"Local/Icons/{ResourceId}.png";
}

/// <summary>一次五星记录。点击行展开期间获得的四星明细。</summary>
public sealed partial class FiveStarCard : ObservableObject
{
    [ObservableProperty]
    private bool _isExpanded;

    [ObservableProperty]
    private bool _isHighlighted;

    public CardPoolType Pool { get; init; }

    public required int ResourceId { get; init; }
    public required string Name { get; init; }
    public required DateTime? Time { get; init; }
    public required string TimeText { get; init; }

    /// <summary>名称下方的说明：记录列表为获取时间，总览为「卡池，日期」。</summary>
    public string Subtitle => SubtitleOverride ?? TimeText;

    public string? SubtitleOverride { get; set; }

    public required int Pity { get; init; }

    /// <summary>true 限定 / false 常驻 / null 无法判断（资料缺失的武器）。</summary>
    public required bool? IsLimited { get; init; }

    public required PityKind Kind { get; init; }

    /// <summary>小保底歪了。</summary>
    public required bool IsLost { get; init; }

    public required IReadOnlyList<FourStarChip> FourStars { get; init; }

    /// <summary>资料图标（随包或从资料仓库下载后缓存）。</summary>
    public string IconPath => $"Local/Icons/{ResourceId}.png";

    public bool IsHardPity => Pity >= PullsSummaryCalculator.HardPity;
    public bool IsLimitedTag => IsLimited == true;
    public bool IsPermanentTag => IsLimited == false;
    public bool IsGuaranteed => Kind == PityKind.Guaranteed;
    public bool IsSoftPity => Kind == PityKind.SoftPity;
    public bool IsStandardPool => Kind == PityKind.Standard;
    public bool HasFourStars => FourStars.Count > 0;
    public bool NoFourStars => FourStars.Count == 0;

    /// <summary>抽数刻度条的比例（满格 80 抽）。</summary>
    public double PityRatio => Math.Min(1, Pity / (double)PullsSummaryCalculator.HardPity);
}

/// <summary>列表首项：自上一个五星起的进度。</summary>
public sealed class NotYetCard
{
    public required int Pity { get; init; }
    public required int ToHardPity { get; init; }
    public required bool? NextIsGuaranteed { get; init; }
    public required string LastPullText { get; init; }
    public required IReadOnlyList<FourStarChip> FourStars { get; init; }
    public required string ToHardPityText { get; init; }
    public bool ShowsNextKind => NextIsGuaranteed is not null;
    public bool HasFourStars => FourStars.Count > 0;
    public bool IsNextGuaranteed => NextIsGuaranteed == true;
    public bool IsNextSoftPity => NextIsGuaranteed == false;
}

/// <summary>
/// 单个卡池的抽卡记录：首项为未出金进度，其后五星记录由新到旧，滚动到底时每次多加载一页。
/// 过滤条件只影响列表，不改变上方统计卡片。
/// </summary>
public sealed partial class PoolRecordsViewModel : ObservableObject
{
    public const int PageSize = 20;

    private readonly PullsSession _session;
    private readonly LocalizationService _localization;
    private List<FiveStarCard> _all = [];
    private List<FiveStarCard> _filtered = [];

    public PoolRecordsViewModel(PullsSession session, LocalizationService localization)
    {
        _session = session;
        _localization = localization;
        _session.SnapshotReplaced += (_, _) => Rebuild();

        TimeRanges =
        [
            new LocalizedOption(0, "Stat_Filter_DateAll", localization),
            new LocalizedOption(1, "Stat_Filter_Date1Month", localization),
            new LocalizedOption(3, "Stat_Filter_Date3Months", localization),
            new LocalizedOption(6, "Pulls_Filter6Months", localization),
        ];
        FiveStarFilters =
        [
            new LocalizedOption(FiveStarFilter.All, "Pulls_FilterAll", localization),
            new LocalizedOption(FiveStarFilter.Limited, "Pulls_Limited", localization),
            new LocalizedOption(FiveStarFilter.Permanent, "Pulls_Permanent", localization),
            new LocalizedOption(FiveStarFilter.Lost, "Pulls_Lost", localization),
        ];
        _selectedTimeRange = TimeRanges[0];
        _selectedFiveStarFilter = FiveStarFilters[0];
    }

    public enum FiveStarFilter { All, Limited, Permanent, Lost }

    public IReadOnlyList<LocalizedOption> TimeRanges { get; }

    public IReadOnlyList<LocalizedOption> FiveStarFilters { get; }

    [ObservableProperty]
    private LocalizedOption _selectedTimeRange;

    [ObservableProperty]
    private LocalizedOption _selectedFiveStarFilter;

    [ObservableProperty]
    private CardPoolType _pool = CardPoolType.CharacterEvent;

    /// <summary>未出金进度卡；卡池没有记录时为 null。</summary>
    [ObservableProperty]
    private NotYetCard? _notYet;

    /// <summary>已显示的五星卡片（分页追加）。</summary>
    public ObservableCollection<FiveStarCard> Items { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LoadedText))]
    private int _loadedCount;

    [ObservableProperty]
    private bool _hasMore;

    public string LoadedText => string.Format(_localization["Pulls_RecordsHint"], LoadedCount);

    partial void OnPoolChanged(CardPoolType value) => Rebuild();

    /// <summary>语言切换后重建（卡片里的文字随语言变化）。</summary>
    public void RefreshTexts()
    {
        OnPropertyChanged(nameof(LoadedText));
        Rebuild();
    }

    partial void OnSelectedTimeRangeChanged(LocalizedOption value) => ApplyFilter();

    partial void OnSelectedFiveStarFilterChanged(LocalizedOption value) => ApplyFilter();

    /// <summary>滚动到底时由视图调用。</summary>
    [RelayCommand]
    public void LoadMore()
    {
        int next = Math.Min(_filtered.Count, Items.Count + PageSize);
        for (int i = Items.Count; i < next; i++) Items.Add(_filtered[i]);
        LoadedCount = Items.Count;
        HasMore = Items.Count < _filtered.Count;
    }

    private void Rebuild()
    {
        PullsSnapshot? snapshot = _session.Snapshot;
        if (snapshot is null || !snapshot.Pools.TryGetValue(Pool, out var result))
        {
            _all = [];
            NotYet = null;
            ApplyFilter();
            return;
        }

        CardPoolStatistics stats = result.PoolStatistics;
        PoolProgress progress = PullsSummaryCalculator.Progress(snapshot, Pool);
        HitGoldData? remainder = stats.HitGoldDatas.FirstOrDefault() is { GachaData.QualityLevel: not 5 } r ? r : null;
        NotYet = stats.Calculate.Tides == 0 ? null : new NotYetCard
        {
            Pity = progress.CurrentPity,
            ToHardPity = progress.PullsToHardPity,
            NextIsGuaranteed = progress.NextIsGuaranteed,
            LastPullText = Format(progress.LastPullAt),
            FourStars = Chips(remainder),
            ToHardPityText = string.Format(_localization["Pulls_ToHardPity"], progress.PullsToHardPity),
        };

        var cards = BuildCards(stats, Pool);
        _all = cards;
        ApplyFilter();
    }


    /// <summary>卡池的五星记录（新→旧）。保底类型按时间顺序推算：歪了之后的下一个五星为大保底。</summary>
    internal static List<FiveStarCard> BuildCards(CardPoolStatistics stats, CardPoolType Pool)
    {
        var chronological = stats.HitGoldDatas.Where(x => x.GachaData.QualityLevel == 5).Reverse().ToList();
        var cards = new List<FiveStarCard>(chronological.Count);
        bool guaranteed = false;
        bool hasPityRule = Pool is CardPoolType.CharacterEvent or CardPoolType.WeaponEvent
            or CardPoolType.CharacterCollaboration or CardPoolType.WeaponCollaboration;
        foreach (HitGoldData hit in chronological)
        {
            bool? limited = hit.IsMiss is null ? null : !hit.IsMiss.Value;
            DateTime? time = Parse(hit.GachaData.Time);
            PityKind kind = !hasPityRule ? PityKind.Standard : guaranteed ? PityKind.Guaranteed : PityKind.SoftPity;
            cards.Add(new FiveStarCard
            {
                Pool = Pool,
                ResourceId = hit.GachaData.ResourceId,
                Name = hit.DisplayName,
                Time = time,
                TimeText = Format(time),
                Pity = hit.Pity,
                IsLimited = limited,
                Kind = kind,
                IsLost = hasPityRule && hit.IsMiss == true,
                FourStars = Chips(hit),
            });
            if (Pool == CardPoolType.CharacterEvent) guaranteed = hit.IsMiss == true;
        }
        cards.Reverse(); // 新→旧
        return cards;
    }

    private void ApplyFilter()
    {
        int months = SelectedTimeRange.Value is int m ? m : 0;
        DateTime? since = months > 0 ? DateTime.Now.AddMonths(-months) : null;
        var filter = SelectedFiveStarFilter.Value is FiveStarFilter f ? f : FiveStarFilter.All;
        _filtered = _all.Where(c =>
                (since is null || c.Time >= since) &&
                filter switch
                {
                    FiveStarFilter.Limited => c.IsLimited == true,
                    FiveStarFilter.Permanent => c.IsLimited == false,
                    FiveStarFilter.Lost => c.IsLost,
                    _ => true,
                })
            .ToList();
        Items.Clear();
        LoadMore();
    }

    private static IReadOnlyList<FourStarChip> Chips(HitGoldData? hit) =>
        hit is null ? [] : hit.FourStarHistories.Select(x => new FourStarChip(x.ResourceId, x.Name, x.Count)).ToList();

    private static DateTime? Parse(string value) =>
        DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime t) ? t : null;

    private static string Format(DateTime? time) => time?.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) ?? "—";
}
