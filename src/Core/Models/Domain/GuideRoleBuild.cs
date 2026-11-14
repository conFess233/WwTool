namespace WwTool.Core.Models.Domain;

/// <summary>
/// 攻略站快照中「我的养成」需要的实际数据（不含推荐方案）。
/// 快照里技能、声骸只保存了图标与等级，没有名称（同步时不保留攻略站的多语言文本）。
/// </summary>
public sealed record GuideRoleBuild(IReadOnlyList<GuideSkillLevel> Skills, IReadOnlyList<GuideEchoSlot> Echoes, IReadOnlyList<string> EchoSetImageUrls)
{
    public static GuideRoleBuild Empty { get; } = new([], [], []);
}

/// <summary>当前账号的一项技能：图标与当前等级。</summary>
public sealed record GuideSkillLevel(string? ImageUrl, int Level);

/// <summary>当前装备的一个声骸槽位：COST 与等级（缺失时为 null）。</summary>
public sealed record GuideEchoSlot(int Cost, int? Level);
