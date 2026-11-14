using WwTool.Core.Models.Domain;
using WwTool.UI.Pages.Events;

namespace WwTool.UI.Pages.Roles;

/// <summary>
/// 详情弹窗「我的养成」页签：攻略站快照里当前账号的实际数据（不含攻略推荐）。
/// 技能与声骸套装只有图标（快照不含名称），图片由详情弹窗统一加载。
/// </summary>
public sealed record CharacterBuild(
    string SyncInfo,
    IReadOnlyList<GuideHoverAttribute> Attributes,
    GuideHoverWeapon? Weapon,
    IReadOnlyList<BuildSkillItem> Skills,
    IReadOnlyList<BuildEchoItem> Echoes,
    IReadOnlyList<BuildSetItem> EchoSets)
{
    public bool HasAttributes => Attributes.Count > 0;
    public bool HasWeapon => Weapon is not null;
    public bool HasSkills => Skills.Count > 0;
    public bool HasEchoes => Echoes.Count > 0;
    public bool HasEchoSets => EchoSets.Count > 0;
    public string WeaponStarsText => Weapon is null ? string.Empty : new string('★', Math.Clamp(Weapon.Star, 1, 5));
    public bool IsEmpty => !HasAttributes && !HasWeapon && !HasSkills && !HasEchoes;

    public IEnumerable<ChallengeImageItem> ImageItems => Skills.Cast<ChallengeImageItem>().Concat(EchoSets);

    public static CharacterBuild From(string syncInfo, GuideRoleHoverDetails details, GuideRoleBuild build) => new(
        syncInfo,
        details.Attributes,
        details.Weapon,
        build.Skills.Select(s => new BuildSkillItem(s.ImageUrl, s.Level)).ToList(),
        build.Echoes.Select(e => new BuildEchoItem(e.Cost, e.Level)).ToList(),
        build.EchoSetImageUrls.Select(u => new BuildSetItem(u)).ToList());
}

public sealed class BuildSkillItem(string? imageUrl, int level) : ChallengeImageItem(imageUrl)
{
    public string LevelText { get; } = "Lv." + level;
}

public sealed class BuildSetItem(string? imageUrl) : ChallengeImageItem(imageUrl);

public sealed class BuildEchoItem(int cost, int? level)
{
    public string CostText { get; } = "COST " + cost;
    public string LevelText { get; } = level is { } l ? "+" + l : "—";
}
