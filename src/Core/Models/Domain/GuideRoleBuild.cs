namespace WwTool.Core.Models.Domain;

/// <summary>
/// 攻略站快照中「我的养成」需要的实际数据（不含推荐方案）。
/// 名称来自快照里的 <c>texts</c>；较早的快照没有保存名称，这时只有图标与等级（重新同步后补上）。
/// </summary>
public sealed record GuideRoleBuild(
    IReadOnlyList<GuideSkillLevel> Skills,
    IReadOnlyList<GuideEchoSlot> Echoes,
    IReadOnlyList<GuideEchoSet> EchoSets,
    string? MainEchoName)
{
    public static GuideRoleBuild Empty { get; } = new([], [], [], null);

    /// <summary>有技能或声骸却没有名称：快照是保存名称之前同步的。</summary>
    public bool LacksNames =>
        Skills.Any(s => s.Name is null) || Echoes.Any(e => e.MainStat is null) || EchoSets.Any(s => s.Name is null);
}

/// <summary>当前账号的一项技能：图标、名称、类型（常态攻击等）与当前等级。</summary>
public sealed record GuideSkillLevel(string? ImageUrl, int Level, string? Name = null, string? Type = null);

/// <summary>当前装备的一个声骸槽位：COST、等级与主属性 / 副属性名称（缺失时为 null）。</summary>
public sealed record GuideEchoSlot(int Cost, int? Level, string? MainStat = null, string? SubStat = null);

/// <summary>当前生效的声骸套装：图标与名称。</summary>
public sealed record GuideEchoSet(string? ImageUrl, string? Name);
