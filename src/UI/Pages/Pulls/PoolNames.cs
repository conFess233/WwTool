using WwTool.Core.Enums;

namespace WwTool.UI.Pages.Pulls;

/// <summary>卡池的语言资源键（沿用 1.x 的键名）与卡池分组。</summary>
public static class PoolNames
{
    /// <summary>卡池全名，例如“角色活动唤取”。</summary>
    public static string TitleKey(CardPoolType pool) => "Enum_CardPoolType_" + pool switch
    {
        CardPoolType.CharacterEvent => "RoleEvent",
        CardPoolType.CharacterStandard => "RolePermanent",
        CardPoolType.WeaponStandard => "WeaponPermanent",
        CardPoolType.Beginner => "Novice",
        CardPoolType.BeginnerChoice => "NoviceSelect",
        CardPoolType.CharacterNoviceJourney => "RoleNewbie",
        CardPoolType.WeaponNoviceJourney => "WeaponNewbie",
        _ => pool.ToString(),
    };

    /// <summary>页签上单独显示的 4 个主卡池（规范 02 §7.11），其余归入“其他卡池”。</summary>
    public static IReadOnlyList<CardPoolType> MainPools { get; } =
        [CardPoolType.CharacterEvent, CardPoolType.WeaponEvent, CardPoolType.CharacterStandard, CardPoolType.WeaponStandard];

    public static bool IsMain(CardPoolType pool) => MainPools.Contains(pool);
}
