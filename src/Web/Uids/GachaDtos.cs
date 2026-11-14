using System.Globalization;
using WwTool.Core.Enums;
using WwTool.Core.Models.Domain;
using WwTool.Core.Models.Statistics;
using WwTool.Core.Services.Gacha;

namespace WwTool.Web.Uids;

// 抽卡统计接口的返回模型。统计全部来自 Core 的 PullsService.Calculate / PullsSummaryCalculator / PullsChartData，
// 与桌面端同一计算链（统计基线约束），这里只做显式映射。

public sealed record GachaOverviewDto(
    string Uid,
    int RecordCount,
    SummaryDto Summary,
    IReadOnlyList<PoolDto> Pools,
    int FourStarCharacters,
    int FourStarWeapons);

public sealed record SummaryDto(
    int TotalPulls, int FiveStars, int Limited, int Permanent,
    double? AveragePerFiveStar, double? AveragePerLimited,
    int Wins, int Misses, double? WinRate, int Astrites);

public sealed record PoolDto(
    int Pool,
    int RecordCount,
    SummaryDto Summary,
    ProgressDto Progress,
    IReadOnlyList<FiveStarDto> FiveStars);

public sealed record ProgressDto(int TotalPulls, int CurrentPity, int PullsToHardPity, bool? NextIsGuaranteed, DateTime? LastPullAt);

/// <summary>一次五星（新→旧）。<see cref="IsMiss"/>：角色活动池 true 为歪、false 为限定；其他卡池为 null。</summary>
public sealed record FiveStarDto(int ResourceId, string Name, string ResourceType, string Time, int Pity, bool? IsMiss, IReadOnlyList<FourStarDto> FourStars);

public sealed record FourStarDto(int ResourceId, string Name, int Count);

public sealed record RecordDto(int ResourceId, string Name, string ResourceType, int Quality, string Time);

public sealed record ChartDto(
    IReadOnlyList<object[]> Daily,
    IReadOnlyList<int> PityBins,
    IReadOnlyList<string> PityLabels,
    double? AveragePity,
    IReadOnlyList<object[]> PoolShares,
    int TotalPulls,
    int Astrites,
    IReadOnlyList<ChartFiveStarDto> FiveStars,
    IReadOnlyList<object[]> Cumulative,
    int FiveStarCount, int FourStarCount, int ThreeStarCount,
    int Wins, int Misses, double? WinRate);

public sealed record ChartFiveStarDto(int Pool, int ResourceId, string Name, DateTime? Time, int Pity, bool? IsLimited);

public static class GachaMapper
{
    public static SummaryDto Map(PullsSummary s) => new(
        s.TotalPulls, s.FiveStars, s.Limited, s.Permanent, s.AveragePerFiveStar, s.AveragePerLimited, s.Wins, s.Misses, s.WinRate, s.Astrites);

    public static GachaOverviewDto Overview(PullsSnapshot snapshot) => new(
        snapshot.Uid,
        snapshot.RecordCount,
        Map(PullsSummaryCalculator.ForAll(snapshot)),
        Enum.GetValues<CardPoolType>().Select(pool =>
        {
            PoolProgress p = PullsSummaryCalculator.Progress(snapshot, pool);
            CardPoolStatistics stats = snapshot.Pools[pool].PoolStatistics;
            return new PoolDto(
                (int)pool,
                snapshot.RecordsByPool[pool].Count,
                Map(PullsSummaryCalculator.ForPool(snapshot, pool)),
                new ProgressDto(p.TotalPulls, p.CurrentPity, p.PullsToHardPity, p.NextIsGuaranteed, p.LastPullAt),
                stats.HitGoldDatas.Where(h => h.GachaData.QualityLevel == 5).Select(FiveStar).ToList());
        }).ToList(),
        // 四星角色与武器：角色资源 ID 为 4 位，武器为 8 位
        snapshot.AllRecords.Count(r => r.QualityLevel == 4 && r.ResourceId < 100_000),
        snapshot.AllRecords.Count(r => r.QualityLevel == 4 && r.ResourceId >= 100_000));

    private static FiveStarDto FiveStar(HitGoldData h) => new(
        h.GachaData.ResourceId, h.GachaData.Name, h.GachaData.ResourceType, h.GachaData.Time, h.Pity, h.IsMiss,
        h.FourStarHistories.Select(f => new FourStarDto(f.ResourceId, f.Name, f.Count)).ToList());

    public static RecordDto Record(GachaPull r) => new(r.ResourceId, r.Name, r.ResourceType, r.QualityLevel, r.Time);

    public static ChartDto Chart(PullsChartData c) => new(
        c.Daily.Select(d => new object[] { d.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), d.Pulls }).ToList(),
        c.PityBins,
        PullsChartData.PityLabels,
        c.AveragePity,
        c.PoolShares.Select(p => new object[] { (int)p.Pool, p.Pulls }).ToList(),
        c.TotalPulls,
        c.Astrites,
        c.FiveStars.Select(f => new ChartFiveStarDto((int)f.Pool, f.ResourceId, f.Name, f.Time, f.Pity, f.IsLimited)).ToList(),
        c.Cumulative.Select(d => new object[] { d.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), d.Pulls }).ToList(),
        c.FiveStarCount, c.FourStarCount, c.ThreeStarCount, c.Wins, c.Misses, c.WinRate);
}
