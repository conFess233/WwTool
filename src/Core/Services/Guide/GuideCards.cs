using WwTool.Core.Enums;

namespace WwTool.Core.Services.Guide;

/// <summary>角色 / 武器卡片排序所需的字段。</summary>
public interface IGuideCard
{
    int Star { get; }
    bool IsLimitedFiveStar { get; }
    DateTime? FirstAcquiredAt { get; }
    int SourceOrder { get; }
}

/// <summary>“首次获取时间”取自哪些卡池的本地抽卡记录。</summary>
public static class GuideCardPools
{
    public static readonly IReadOnlyList<CardPoolType> Roles =
    [
        CardPoolType.CharacterEvent, CardPoolType.CharacterStandard, CardPoolType.Beginner,
        CardPoolType.BeginnerChoice, CardPoolType.BeginnerChoiceGiveback, CardPoolType.CharacterNoviceJourney,
        CardPoolType.CharacterCollaboration, CardPoolType.CharacterMemoryJourney,
    ];

    public static readonly IReadOnlyList<CardPoolType> Weapons =
    [
        CardPoolType.WeaponEvent, CardPoolType.WeaponStandard,
        CardPoolType.WeaponNoviceJourney, CardPoolType.WeaponCollaboration, CardPoolType.WeaponMemoryJourney,
    ];
}

/// <summary>排序方式。三种都先按“限定五星 → 五星 → 四星 → 其他”分组（与 1.x 一致）。</summary>
public enum GuideSort
{
    Star,
    Name,
    AcquiredTime,
}

public static class GuideCardSorter
{
    public static IReadOnlyList<T> Sort<T>(IEnumerable<T> items, GuideSort sort, Func<T, string> name) where T : IGuideCard =>
        sort switch
        {
            GuideSort.Name => items.OrderBy(x => GroupRank(x)).ThenBy(name, StringComparer.CurrentCulture).ThenBy(x => x.SourceOrder).ToArray(),
            // 只有限定五星有可靠的首次获取时间；有时间的在前、新的在前。
            GuideSort.AcquiredTime => items.OrderBy(x => GroupRank(x))
                .ThenBy(x => x.IsLimitedFiveStar && x.FirstAcquiredAt.HasValue ? 0 : 1)
                .ThenByDescending(x => x.IsLimitedFiveStar ? x.FirstAcquiredAt : null)
                .ThenBy(x => x.SourceOrder)
                .ToArray(),
            _ => items.OrderBy(x => GroupRank(x)).ThenBy(x => x.SourceOrder).ToArray(),
        };

    private static int GroupRank(IGuideCard item) => item switch
    {
        { Star: 5, IsLimitedFiveStar: true } => 0,
        { Star: 5 } => 1,
        { Star: 4 } => 2,
        _ => 3,
    };
}
