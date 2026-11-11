using System.Globalization;
using WwTool.Core.Enums;
using WwTool.Core.Models.Domain;
using WwTool.Core.Models.Statistics;

namespace WwTool.Core.Services.Gacha;

/// <summary>详细图表的筛选：卡池（null 为全部）与起始时间（null 为全部记录）。</summary>
public sealed record PullsChartFilter(IReadOnlySet<CardPoolType>? Pools, DateTime? Since);

public sealed record DailyPulls(DateTime Date, int Pulls);

public sealed record PoolShare(CardPoolType Pool, int Pulls);

/// <summary>一个五星及其花费抽数。<see cref="IsLimited"/> 为 null 表示资料无法判断。</summary>
public sealed record FiveStarCost(CardPoolType Pool, int ResourceId, string Name, DateTime? Time, int Pity, bool? IsLimited);

public sealed record CumulativePulls(DateTime Date, int Pulls);

/// <summary>详细图表页的全部数据。</summary>
public sealed class PullsChartData
{
    public static readonly IReadOnlyList<string> PityLabels = ["1-10", "11-20", "21-30", "31-40", "41-50", "51-60", "61-70", "71-80"];

    /// <summary>范围内每一天（含无抽卡的日子，补 0）。</summary>
    public required IReadOnlyList<DailyPulls> Daily { get; init; }
    public int DailyPeak => Daily.Count == 0 ? 0 : Daily.Max(x => x.Pulls);

    /// <summary>五星抽数分布，8 个区间（与 <see cref="PityLabels"/> 对应）。</summary>
    public required IReadOnlyList<int> PityBins { get; init; }
    public required double? AveragePity { get; init; }

    public required IReadOnlyList<PoolShare> PoolShares { get; init; }
    public required int TotalPulls { get; init; }
    public int Astrites => TotalPulls * PullsSummaryCalculator.AstritesPerPull;

    /// <summary>范围内的五星，新→旧。</summary>
    public required IReadOnlyList<FiveStarCost> FiveStars { get; init; }

    /// <summary>按日累计抽数（只含有抽卡的日子）。</summary>
    public required IReadOnlyList<CumulativePulls> Cumulative { get; init; }

    public required int FiveStarCount { get; init; }
    public required int FourStarCount { get; init; }
    public required int ThreeStarCount { get; init; }

    /// <summary>角色活动池在范围内的小保底胜负；不含角色活动池时为 0。</summary>
    public required int Wins { get; init; }
    public required int Misses { get; init; }
    public double? WinRate => Wins + Misses == 0 ? null : Wins * 100d / (Wins + Misses);

    public bool IsEmpty => TotalPulls == 0;
}

/// <summary>
/// 从 <see cref="PullsSnapshot"/> 生成详细图表数据。每个五星的抽数取自统计结果（按完整历史计算），
/// 时间筛选只决定哪些五星 / 记录进入图表，不会把跨越起始时间的保底截短。
/// </summary>
public static class PullsChartBuilder
{
    public static PullsChartData Build(PullsSnapshot snapshot, PullsChartFilter filter)
    {
        IReadOnlyList<CardPoolType> pools = Enum.GetValues<CardPoolType>()
            .Where(p => filter.Pools is null || filter.Pools.Contains(p))
            .ToList();
        DateTime? since = filter.Since;

        // 记录：卡池 + 时间筛选。
        var records = new List<(CardPoolType Pool, DateTime Time, int Quality)>();
        foreach (CardPoolType pool in pools)
        {
            foreach (GachaPull r in snapshot.RecordsByPool[pool])
            {
                DateTime? time = Parse(r.Time);
                if (time is null || (since is not null && time < since)) continue;
                records.Add((pool, time.Value, r.QualityLevel));
            }
        }

        // 五星：取统计结果中的抽数与限定判断；角色活动池按时间顺序推算小保底胜负。
        var fiveStars = new List<FiveStarCost>();
        int wins = 0, misses = 0;
        foreach (CardPoolType pool in pools)
        {
            bool guaranteed = false;
            IEnumerable<HitGoldData> chronological = snapshot.Pools[pool].PoolStatistics.HitGoldDatas
                .Where(x => x.GachaData.QualityLevel == 5)
                .Reverse();
            foreach (HitGoldData hit in chronological)
            {
                DateTime? time = Parse(hit.GachaData.Time);
                bool inRange = since is null || time >= since;
                // 与 GachaStatisticsService.OrganizeData 相同：限定且不在大保底上算赢，非限定算歪。
                if (pool == CardPoolType.CharacterEvent && hit.IsMiss is { } miss)
                {
                    if (inRange)
                    {
                        if (miss) misses++;
                        else if (!guaranteed) wins++;
                    }
                    guaranteed = miss;
                }
                if (!inRange) continue;
                fiveStars.Add(new FiveStarCost(pool, hit.GachaData.ResourceId, hit.DisplayName, time, hit.Pity,
                    hit.IsMiss is null ? null : !hit.IsMiss.Value));
            }
        }
        fiveStars.Sort((a, b) => Nullable.Compare(b.Time, a.Time));

        int[] bins = new int[PullsChartData.PityLabels.Count];
        foreach (FiveStarCost f in fiveStars) bins[Math.Clamp((f.Pity - 1) / 10, 0, bins.Length - 1)]++;

        var daily = new List<DailyPulls>();
        var cumulative = new List<CumulativePulls>();
        if (records.Count > 0)
        {
            var byDay = records.GroupBy(x => x.Time.Date).ToDictionary(g => g.Key, g => g.Count());
            DateTime first = since?.Date ?? byDay.Keys.Min();
            DateTime last = byDay.Keys.Max();
            for (DateTime d = first; d <= last; d = d.AddDays(1)) daily.Add(new DailyPulls(d, byDay.GetValueOrDefault(d)));
            int running = 0;
            foreach (var (day, count) in byDay.OrderBy(x => x.Key))
            {
                running += count;
                cumulative.Add(new CumulativePulls(day, running));
            }
        }

        return new PullsChartData
        {
            Daily = daily,
            PityBins = bins,
            AveragePity = fiveStars.Count == 0 ? null : fiveStars.Average(x => x.Pity),
            PoolShares = pools.Select(p => new PoolShare(p, records.Count(r => r.Pool == p))).Where(x => x.Pulls > 0).ToList(),
            TotalPulls = records.Count,
            FiveStars = fiveStars,
            Cumulative = cumulative,
            FiveStarCount = records.Count(r => r.Quality == 5),
            FourStarCount = records.Count(r => r.Quality == 4),
            ThreeStarCount = records.Count(r => r.Quality <= 3),
            Wins = wins,
            Misses = misses,
        };
    }

    private static DateTime? Parse(string value) =>
        DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime t) ? t : null;
}
