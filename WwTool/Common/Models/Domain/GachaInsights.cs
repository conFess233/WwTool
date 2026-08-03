using WwTool.Common.Enums;

namespace WwTool.Common.Models.Domain;

public sealed class GachaInsights
{
    public const double FeaturedExpectation = 83.33d;

    public IReadOnlyList<int> PityDistribution { get; init; } = [];
    public IReadOnlyList<string> PityLabels { get; init; } = [];
    public IReadOnlyList<FiveStarInsight> FiveStars { get; init; } = [];
    public IReadOnlyList<PoolRarityInsight> PoolRarities { get; init; } = [];
    public IReadOnlyList<DailyPullInsight> DailyPulls { get; init; } = [];
    public IReadOnlyList<CumulativePullInsight> CumulativePulls { get; init; } = [];
    public IReadOnlyList<FeaturedPullInsight> FeaturedPulls { get; init; } = [];
    public int CurrentCharacterPity { get; init; }
}

public sealed record FiveStarInsight(DateTime OccurredAt, string Name, int Pity, bool? IsFeatured);
public sealed record PoolRarityInsight(CardPoolType PoolType, int ThreeStar, int FourStar, int FiveStar);
public sealed record DailyPullInsight(DateTime Date, int Pulls);
public sealed record CumulativePullInsight(DateTime Date, int Pulls, int FiveStars);
public sealed record FeaturedPullInsight(
    int Index,
    string Name,
    int CumulativePulls,
    double ExpectedCumulativePulls,
    double RunningAverage,
    bool IsIncomplete);
