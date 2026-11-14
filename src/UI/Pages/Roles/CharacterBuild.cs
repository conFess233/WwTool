using CommunityToolkit.Mvvm.ComponentModel;
using WwTool.Core.Models.Domain;
using WwTool.UI.Pages.Events;

namespace WwTool.UI.Pages.Roles;

/// <summary>
/// 详情弹窗「我的养成」页签：攻略站快照里当前账号的实际数据（不含攻略推荐）。
/// 技能与声骸套装的图标由详情弹窗统一加载；较早的快照没有名称，只显示图标与等级。
/// </summary>
public sealed record CharacterBuild(
    string SyncInfo,
    IReadOnlyList<GuideHoverAttribute> Attributes,
    GuideHoverWeapon? Weapon,
    IReadOnlyList<BuildSkillItem> Skills,
    IReadOnlyList<BuildEchoItem> Echoes,
    IReadOnlyList<BuildSetItem> EchoSets,
    string? MainEchoName,
    bool LacksNames)
{
    public bool HasAttributes => Attributes.Count > 0;
    public bool HasWeapon => Weapon is not null;
    public bool HasSkills => Skills.Count > 0;
    public bool HasEchoes => Echoes.Count > 0;
    public bool HasEchoSets => EchoSets.Count > 0;
    public bool HasMainEcho => MainEchoName is not null;
    public string WeaponStarsText => Weapon is null ? string.Empty : new string('★', Math.Clamp(Weapon.Star, 1, 5));

    public IEnumerable<ChallengeImageItem> ImageItems => Skills.Cast<ChallengeImageItem>().Concat(EchoSets);

    public static CharacterBuild From(string syncInfo, GuideRoleHoverDetails details, GuideRoleBuild build) => new(
        syncInfo,
        details.Attributes,
        details.Weapon,
        build.Skills.Select(s => new BuildSkillItem(s.ImageUrl, s.Level, s.Name, s.Type)).ToList(),
        build.Echoes.Select(e => new BuildEchoItem(e.Cost, e.Level, e.MainStat, e.SubStat)).ToList(),
        build.EchoSets.Select(s => new BuildSetItem(s.ImageUrl, s.Name)).ToList(),
        build.MainEchoName,
        build.LacksNames);
}

public sealed partial class BuildSkillItem(string? imageUrl, int level, string? name, string? type) : ChallengeImageItem(imageUrl)
{
    /// <summary>图标来自 encore.moe（白色线稿，放在深色圆底上）；否则是攻略站的图标，放在浅色底上。</summary>
    [ObservableProperty] private bool _usesEncoreIcon;

    public string LevelText { get; } = "Lv." + level;
    public string? Name { get; } = name;
    public string? Type { get; } = type;
    public bool HasName => Name is not null;

    /// <summary>悬停提示：「常态攻击 · 化弦为剑」。</summary>
    public string? Tip { get; } = name is null ? type : type is null ? name : type + " · " + name;
}

public sealed class BuildSetItem(string? imageUrl, string? name) : ChallengeImageItem(imageUrl)
{
    public string? Name { get; } = name;
    public bool HasName => Name is not null;
}

public sealed class BuildEchoItem(int cost, int? level, string? mainStat, string? subStat)
{
    public string CostText { get; } = "COST " + cost;
    public string LevelText { get; } = level is { } l ? "+" + l : "—";
    public string? MainStat { get; } = mainStat;
    public string? SubStat { get; } = subStat;
    public bool HasMainStat => MainStat is not null;
    public bool HasSubStat => SubStat is not null;
}
