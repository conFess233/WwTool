using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using WwTool.Core.Services.Notices;

namespace WwTool.Core.Services.Challenges;

/// <summary>
/// 解析 encore.moe 角色详情接口（<c>docs/API/Encore.md</c>「角色详情」）。字段缺失或类型不对时跳过该项；
/// 整体不是对象或没有 Id 时抛 <see cref="JsonException"/>。
/// </summary>
public static partial class EncoreCharacterParser
{
    /// <summary>接口里以 <c>/Game/</c> 开头的资源路径拼在这个地址后面。</summary>
    public const string DefaultResourceBase = "https://api-v2.encore.moe/resource/Data";

    /// <summary>档案里取的键，按显示顺序。</summary>
    public static readonly IReadOnlyList<string> ProfileKeys =
        ["Birthday", "Sex", "Country", "Influence", "CVNameCn", "CVNameJp", "CVNameEn", "CVNameKo"];

    public static EncoreCharacter Parse(string json, string resourceBase = DefaultResourceBase)
    {
        using JsonDocument doc = JsonDocument.Parse(json);
        JsonElement r = doc.RootElement;
        if (r.ValueKind != JsonValueKind.Object || Int(r, "Id") is not { } id) throw new JsonException("不是角色详情");

        var skins = new List<EncoreSkin>();
        foreach (JsonElement s in Array(r, "Skins"))
        {
            if (Int(s, "Id") is not { } skinId) continue;
            skins.Add(new EncoreSkin(skinId, Str(s, "Name") ?? string.Empty,
                Url(Str(s, "FormationRoleCard"), resourceBase) ?? Url(Str(s, "PreviewRoleCard"), resourceBase)));
        }
        skins.Sort((a, b) => a.Id.CompareTo(b.Id));

        var tags = new List<EncoreTag>();
        foreach (JsonElement t in Array(r, "Tags"))
        {
            if (Str(t, "TagName") is { Length: > 0 } name) tags.Add(new EncoreTag(name, Str(t, "TagDesc"), Str(t, "TagNameColor")));
        }

        var profile = new List<EncoreProfileField>();
        if (r.TryGetProperty("favorRole", out JsonElement favor) && favor.ValueKind == JsonValueKind.Object)
        {
            foreach (string key in ProfileKeys)
            {
                if (Plain(Content(favor, key)) is { } value) profile.Add(new EncoreProfileField(key, value));
            }
        }

        var stats = new List<EncoreStat>();
        foreach (JsonElement p in Array(r, "Properties"))
        {
            if (Str(p, "Name") is not { Length: > 0 } name) continue;
            var levels = new List<EncoreStatLevel>();
            foreach (JsonElement g in Array(p, "GrowthValues"))
            {
                if (Int(g, "level") is not { } level || !g.TryGetProperty("value", out JsonElement v)) continue;
                string? text = StatText(v);
                if (text is null) continue;
                // 同一等级出现两次（突破前 / 后）时取后者。
                if (levels.Count > 0 && levels[^1].Level == level) levels[^1] = new EncoreStatLevel(level, text);
                else levels.Add(new EncoreStatLevel(level, text));
            }
            if (levels.Count > 0) stats.Add(new EncoreStat(name, levels));
        }

        var skills = new List<EncoreSkill>();
        foreach (JsonElement s in Array(r, "Skills"))
        {
            if (Str(s, "SkillName") is not { Length: > 0 } name) continue;
            var attributes = new List<EncoreSkillAttribute>();
            foreach (JsonElement a in Array(s, "SkillAttributes"))
            {
                if (Str(a, "attributeName") is not { Length: > 0 } attrName) continue;
                string[] values = Array(a, "values").Select(v => v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : v.ToString()).ToArray();
                if (values.Length > 0) attributes.Add(new EncoreSkillAttribute(attrName, values));
            }
            skills.Add(new EncoreSkill(Str(s, "SkillType") ?? string.Empty, name, Rich(Str(s, "SkillDescribe")), Url(Str(s, "Icon"), resourceBase), attributes));
        }

        var chain = new List<EncoreChainNode>();
        foreach (JsonElement c in Array(r, "ResonantChain"))
        {
            if (Str(c, "NodeName") is not { Length: > 0 } name) continue;
            int index = Int(c, "GroupIndex") ?? chain.Count + 1;
            chain.Add(new EncoreChainNode(index, name, Rich(Str(c, "AttributesDescription")), Url(Str(c, "NodeIcon"), resourceBase)));
        }
        chain.Sort((a, b) => a.Index.CompareTo(b.Index));

        return new EncoreCharacter(
            id,
            Content(r, "Name") ?? id.ToString(CultureInfo.InvariantCulture),
            Plain(Content(r, "NickName")),
            Plain(Content(r, "Introduction")),
            Int(r, "QualityId") ?? 0,
            Int(r, "ElementId") ?? -1,
            NullIfEmpty(Str(r, "ElementName")),
            NullIfEmpty(Str(r, "WeaponTypeName")),
            // 与 encore.moe 一致：编队立绘优先，没有时用活动立绘。
            Url(Str(r, "FormationRoleCard"), resourceBase) ?? Url(Str(r, "RolePortrait"), resourceBase) ?? skins.FirstOrDefault()?.PortraitUrl,
            tags,
            skins,
            profile,
            stats,
            skills,
            chain,
            Texts(r, "Stories"),
            Texts(r, "Words"));
    }

    /// <summary>
    /// 资源地址：http(s) 原样返回；<c>/Game/…</c> 拼到 <paramref name="resourceBase"/> 后（只接受带图片扩展名的路径，
    /// 虚幻引擎对象路径如 <c>T_Icon.T_Icon</c> 没有对应文件）。
    /// </summary>
    public static string? Url(string? path, string resourceBase = DefaultResourceBase)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;
        if (path.StartsWith("https://", StringComparison.OrdinalIgnoreCase) || path.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
            return path;
        if (!path.StartsWith('/')) return null;
        string ext = Path.GetExtension(path).ToLowerInvariant();
        if (ext is not (".webp" or ".png" or ".jpg" or ".jpeg")) return null;
        return resourceBase.TrimEnd('/') + path.Replace("/UiResources/", "/UIResources/", StringComparison.Ordinal);
    }

    private static List<EncoreText> Texts(JsonElement r, string name)
    {
        var list = new List<EncoreText>();
        foreach (JsonElement e in Array(r, name).OrderBy(e => Int(e, "Sort") ?? 0))
        {
            if (Str(e, "Title") is { Length: > 0 } title && Str(e, "Content") is { Length: > 0 } content)
                list.Add(new EncoreText(Plain(title) ?? title, Rich(content)!));
        }
        return list;
    }

    // 原数据里有引号没闭合的标签：<span class="font-bold text-3xl style="color:aliceblue;">。
    [GeneratedRegex(@"(<span\s+class=""[^""<>]*?)\s+style=""")]
    private static partial Regex BrokenSpanRegex();

    /// <summary>富文本描述：修正原数据里的残缺标签，其余交给界面的 NoticeHtml 解析。</summary>
    public static string? Rich(string? html) =>
        string.IsNullOrWhiteSpace(html) ? null : BrokenSpanRegex().Replace(html, "$1\" style=\"");

    /// <summary>只显示纯文字的字段（简介、档案）：去掉 &lt;te href&gt; 等游戏内链接标签。</summary>
    public static string? Plain(string? html) =>
        string.IsNullOrWhiteSpace(html) ? null : NullIfEmpty(NoticeHtml.ToPlainText(Rich(html)!.Replace("\n", "<br>")).Trim());

    /// <summary>数值截去小数（与游戏面板一致）；百分比等文字原样。</summary>
    private static string? StatText(JsonElement v) => v.ValueKind switch
    {
        JsonValueKind.Number when v.TryGetDouble(out double d) => Math.Truncate(d).ToString("#,0", CultureInfo.InvariantCulture),
        JsonValueKind.String => NullIfEmpty(v.GetString()),
        _ => null,
    };

    /// <summary><c>{Title, Content}</c> 形式的字段取 Content。</summary>
    private static string? Content(JsonElement e, string name) =>
        e.TryGetProperty(name, out JsonElement v) && v.ValueKind == JsonValueKind.Object ? Str(v, "Content") : null;

    private static IEnumerable<JsonElement> Array(JsonElement e, string name) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out JsonElement v) && v.ValueKind == JsonValueKind.Array ? v.EnumerateArray() : [];

    private static string? Str(JsonElement e, string name) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out JsonElement v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static int? Int(JsonElement e, string name) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out JsonElement v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out int i) ? i : null;

    private static string? NullIfEmpty(string? s) => string.IsNullOrWhiteSpace(s) ? null : s;
}
