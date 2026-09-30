namespace WwTool.Common.Models.Domain;

/// <summary>最近一次攻略站快照中的实际属性和装备摘要。</summary>
public sealed record GuideRoleHoverDetails(
    IReadOnlyList<GuideHoverAttribute> Attributes, GuideHoverWeapon? Weapon);

/// <summary>保留服务端实际值的显示形式，不将缺失值转换为零。</summary>
public sealed record GuideHoverAttribute(string Name, string Value);

/// <summary>当前装备武器的简短信息。</summary>
public sealed record GuideHoverWeapon(string Name, string? ImagePath, int Star,
    IReadOnlyList<GuideHoverAttribute> Fields);
