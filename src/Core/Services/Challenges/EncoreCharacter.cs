namespace WwTool.Core.Services.Challenges;

/// <summary>
/// encore.moe 的角色详情（<c>/{lang}/character/{id}</c>），只保留详情弹窗要用的部分。
/// 描述类文字（<see cref="EncoreSkill.Description"/> 等）保留原始富文本，界面用 <c>NoticeHtml</c> 解析显示。
/// </summary>
public sealed record EncoreCharacter(
    int Id,
    string Name,
    string? NickName,
    string? Introduction,
    int Quality,
    int ElementId,
    string? ElementName,
    string? WeaponTypeName,
    string? PortraitUrl,
    IReadOnlyList<EncoreTag> Tags,
    IReadOnlyList<EncoreSkin> Skins,
    IReadOnlyList<EncoreProfileField> Profile,
    IReadOnlyList<EncoreStat> Stats,
    IReadOnlyList<EncoreSkill> Skills,
    IReadOnlyList<EncoreChainNode> Chain,
    IReadOnlyList<EncoreText> Stories,
    IReadOnlyList<EncoreText> Voices);

/// <summary>定位标签（如「主力输出」），<see cref="Color"/> 为 RRGGBB。</summary>
public sealed record EncoreTag(string Name, string? Description, string? Color);

/// <summary>皮肤与对应的立绘图。</summary>
public sealed record EncoreSkin(int Id, string Name, string? PortraitUrl);

/// <summary>档案的一项。<see cref="Key"/> 为固定键：Birthday、Sex、Country、Influence、CVNameCn、CVNameJp、CVNameEn、CVNameKo。</summary>
public sealed record EncoreProfileField(string Key, string Value);

/// <summary>基础属性及各等级的数值（同一等级突破前后取突破后的值）。</summary>
public sealed record EncoreStat(string Name, IReadOnlyList<EncoreStatLevel> Levels)
{
    public string? ValueAt(int level) => Levels.LastOrDefault(l => l.Level <= level)?.Value ?? Levels.FirstOrDefault()?.Value;
}

public sealed record EncoreStatLevel(int Level, string Value);

/// <summary>技能：类型（常态攻击等）、名称、富文本描述与各技能等级的倍率。</summary>
public sealed record EncoreSkill(string Type, string Name, string? Description, string? IconUrl, IReadOnlyList<EncoreSkillAttribute> Attributes)
{
    /// <summary>最高技能等级（倍率表的列数）；没有倍率时为 0。</summary>
    public int MaxLevel => Attributes.Count == 0 ? 0 : Attributes.Max(a => a.Values.Count);
}

public sealed record EncoreSkillAttribute(string Name, IReadOnlyList<string> Values)
{
    public string? ValueAt(int level) => Values.Count == 0 ? null : Values[Math.Clamp(level, 1, Values.Count) - 1];
}

/// <summary>共鸣链节点（<see cref="Index"/> 为 1–6）。</summary>
public sealed record EncoreChainNode(int Index, string Name, string? Description, string? IconUrl);

/// <summary>故事或语音：标题与富文本内容（语音只显示台词文字）。</summary>
public sealed record EncoreText(string Title, string Content);
