using System.Globalization;
using WwTool.Core.Enums;
using WwTool.Core.Models.Statistics;

namespace WwTool.Core.Services.Gacha;

/// <summary>统计卡片显示的汇总数字（一个卡池或全部卡池）。</summary>
public sealed record PullsSummary(
    int TotalPulls,
    int FiveStars,
    int Limited,
    int Permanent,
    double? AveragePerFiveStar,
    double? AveragePerLimited,
    int Wins,
    int Misses,
    double? WinRate,
    int Astrites);

/// <summary>某卡池自上一个五星起的进度。</summary>
public sealed record PoolProgress(
    CardPoolType Pool,
    int TotalPulls,
    int CurrentPity,
    int PullsToHardPity,
    bool? NextIsGuaranteed,
    HitGoldData? LatestFiveStar,
    DateTime? LastPullAt);

/// <summary>
/// 由 <see cref="PullsSnapshot"/> 派生界面汇总。只读取统计结果，不改变 1.x 的统计口径：
/// 单卡池的平均每金、角色活动池的平均每限定与不歪率与 <see cref="GachaStatisticsService"/> 完全一致；
/// “全部卡池”的平均值是各卡池已完成区间的合并。
/// </summary>
public static class PullsSummaryCalculator
{
    /// <summary>所有卡池的硬保底抽数。</summary>
    public const int HardPity = 80;

    /// <summary>折合星声：每抽 160。</summary>
    public const int AstritesPerPull = 160;

    public static PullsSummary ForPool(PullsSnapshot snapshot, CardPoolType pool) => Summarize(snapshot, [pool]);

    public static PullsSummary ForAll(PullsSnapshot snapshot) => Summarize(snapshot, snapshot.Pools.Keys);

    public static PoolProgress Progress(PullsSnapshot snapshot, CardPoolType pool)
    {
        CardPoolStatistics stats = snapshot.Pools[pool].PoolStatistics;
        HitGoldData? first = stats.HitGoldDatas.FirstOrDefault();
        int pity = first is { GachaData.QualityLevel: not 5 } ? first.Pity : 0;
        HitGoldData? latestFiveStar = stats.HitGoldDatas.FirstOrDefault(x => x.GachaData.QualityLevel == 5);
        bool? guaranteed = pool == CardPoolType.CharacterEvent ? latestFiveStar?.IsMiss == true : null;
        DateTime? lastPull = first is not null && DateTime.TryParse(first.GachaData.Time, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime t)
            ? t
            : null;
        return new PoolProgress(pool, stats.Calculate.Tides, pity, Math.Max(0, HardPity - pity), guaranteed, latestFiveStar, lastPull);
    }

    private static PullsSummary Summarize(PullsSnapshot snapshot, IEnumerable<CardPoolType> pools)
    {
        int total = 0, fiveStars = 0, limited = 0, permanent = 0, completed = 0, limitedSpan = 0;
        bool includesCharacterEvent = false;
        foreach (CardPoolType pool in pools)
        {
            includesCharacterEvent |= pool == CardPoolType.CharacterEvent;
            CardPoolStatistics stats = snapshot.Pools[pool].PoolStatistics;
            List<HitGoldData> golds = stats.HitGoldDatas.Where(x => x.GachaData.QualityLevel == 5).ToList(); // 新→旧
            int remainder = Progress(snapshot, pool).CurrentPity;

            total += stats.Calculate.Tides;
            fiveStars += stats.Calculate.HitGoldCount;
            completed += stats.Calculate.Tides - remainder;
            limited += golds.Count(x => x.IsMiss == false);
            permanent += golds.Count(x => x.IsMiss == true);
            // 与全局统计相同：只计到最近一次限定为止，之后尚未完成的区间不计。
            limitedSpan += golds.SkipWhile(x => x.IsMiss != false).Sum(x => x.Pity);
        }

        int wins = 0, misses = 0;
        double? winRate = null;
        if (includesCharacterEvent)
        {
            Abstractions.GachaStatisticsResult character = snapshot.Pools[CardPoolType.CharacterEvent];
            wins = character.SuccessCount;
            misses = character.MissCount;
            if (wins + misses > 0) winRate = snapshot.Global.SuccessRate;
        }

        return new PullsSummary(
            total,
            fiveStars,
            limited,
            permanent,
            fiveStars > 0 ? (double)completed / fiveStars : null,
            limited > 0 ? (double)limitedSpan / limited : null,
            wins,
            misses,
            winRate,
            total * AstritesPerPull);
    }
}
