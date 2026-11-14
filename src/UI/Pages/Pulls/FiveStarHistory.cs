using Avalonia.Media;
using WwTool.Core.Services.Gacha;

namespace WwTool.UI.Pages.Pulls;

/// <summary>出金抽数的评价，从欧到非。</summary>
public enum PityRating
{
    /// <summary>1–40 抽。</summary>
    Lucky,

    /// <summary>41–60 抽。</summary>
    Steady,

    /// <summary>61–70 抽。</summary>
    Unlucky,

    /// <summary>71–80 抽。</summary>
    Worst,
}

/// <summary>评价的分档、名称与颜色（时间线的外圈与轨道点、详情弹窗的抽取记录共用）。</summary>
public static class PityRatings
{
    /// <summary>各档的抽数上限（含）。</summary>
    public static readonly IReadOnlyList<int> Limits = [40, 60, 70, PullsSummaryCalculator.HardPity];

    // 取中等明度，浅色与深色主题上都看得清；四档明度也不同，不只靠色相区分。
    private static readonly IBrush[] Brushes =
    [
        new SolidColorBrush(Color.Parse("#3E8E5A")),
        new SolidColorBrush(Color.Parse("#C0902E")),
        new SolidColorBrush(Color.Parse("#D2702E")),
        new SolidColorBrush(Color.Parse("#C24A36")),
    ];

    public static PityRating Of(int pity) => pity <= 40 ? PityRating.Lucky : pity <= 60 ? PityRating.Steady : pity <= 70 ? PityRating.Unlucky : PityRating.Worst;

    public static IBrush Brush(PityRating rating) => Brushes[(int)rating];

    public static IBrush Brush(int pity) => Brush(Of(pity));

    /// <summary>名称的语言资源键，如 <c>Pulls_Rating_Lucky</c>。</summary>
    public static string NameKey(PityRating rating) => "Pulls_Rating_" + rating;

    /// <summary>抽数范围，如「41–60」。</summary>
    public static string Range(PityRating rating)
    {
        int i = (int)rating;
        int low = i == 0 ? 1 : Limits[i - 1] + 1;
        return $"{low}–{Limits[i]}";
    }
}

/// <summary>全部卡池的五星记录，供时间线与角色详情使用。</summary>
public static class FiveStarHistory
{
    /// <summary>所有卡池的五星，按获得时间从早到晚；没有时间的排在最前。</summary>
    public static IReadOnlyList<FiveStarCard> All(PullsSnapshot snapshot) =>
        snapshot.Pools
            .SelectMany(kv => PoolRecordsViewModel.BuildCards(kv.Value.PoolStatistics, kv.Key))
            .OrderBy(c => c.Time ?? DateTime.MinValue)
            .ToList();

    /// <summary>武器的资源 ID 为 8 位（如 21010016），角色为 4 位。</summary>
    public static bool IsWeapon(int resourceId) => resourceId >= 100_000;
}
