using System.Globalization;
using System.Text.Json;

namespace WwTool.Core.Services.Challenges;

/// <summary>
/// 解析 encore.moe 的深塔、海墟、矩阵接口（<c>docs/API/Encore.md</c>）。只取界面要用的字段；
/// 字段缺失或类型不对时跳过该项，不抛异常（整体不是 JSON 时抛 <see cref="JsonException"/>）。
/// </summary>
public static class EncoreParser
{
    /// <summary>各期日期都按服务器时间 04:00（UTC+8）切换。</summary>
    public static readonly TimeSpan ServerOffset = TimeSpan.FromHours(8);
    public static readonly TimeSpan ResetTime = TimeSpan.FromHours(4);

    // ── 各期列表 ──────────────────────────────────────────

    public static IReadOnlyList<ChallengeSeason> ParseSeasons(ChallengeKind kind, string json)
    {
        using JsonDocument doc = JsonDocument.Parse(json);
        JsonElement root = doc.RootElement;
        var list = new List<ChallengeSeason>();
        switch (kind)
        {
            case ChallengeKind.Tower:
                if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("seasons", out JsonElement seasons) || seasons.ValueKind != JsonValueKind.Array) break;
                foreach (JsonElement s in seasons.EnumerateArray())
                {
                    if (Int(s, "id") is not { } id || id <= 0) continue;
                    list.Add(new ChallengeSeason(kind, id, Str(s, "name") ?? $"Season {id}", Date(s, "start"), Date(s, "finish")));
                }
                break;
            case ChallengeKind.Wastes:
                foreach (JsonElement s in Array(root))
                {
                    if (Int(s, "Season") is not { } id || id <= 0) continue;
                    list.Add(new ChallengeSeason(kind, id, Str(s, "Name") ?? $"Season {id}", Date(s, "start"), Date(s, "finish")));
                }
                break;
            case ChallengeKind.Matrix:
                foreach (JsonElement s in Array(root))
                {
                    if (Int(s, "Season") is not { } id || id <= 0) continue;
                    list.Add(new ChallengeSeason(kind, id, Str(s, "Name") ?? Str(s, "CycleName") ?? id.ToString(CultureInfo.InvariantCulture), null, null,
                        NullIfEmpty(Str(s, "SeasonName")), NullIfEmpty(Str(s, "EndVersion"))));
                }
                break;
        }
        // 深塔、海墟只保留有日期的期（第 0 期是常驻 / 无日期）。
        if (kind != ChallengeKind.Matrix) list.RemoveAll(s => !s.IsDated);
        list.Sort((a, b) => a.Id.CompareTo(b.Id));
        return list;
    }

    /// <summary>
    /// 默认显示的一期：有日期的取正在进行的一期（没有时取已开始的最后一期，再没有取第一期）；
    /// 矩阵没有日期，取列表最后一项。
    /// </summary>
    public static ChallengeSeason? Current(IReadOnlyList<ChallengeSeason> seasons, DateTimeOffset now)
    {
        if (seasons.Count == 0) return null;
        if (!seasons.Any(s => s.IsDated)) return seasons[^1];
        return seasons.FirstOrDefault(s => s.IsActive(now))
            ?? seasons.LastOrDefault(s => s.Start is { } start && start <= now)
            ?? seasons[0];
    }

    private static DateTimeOffset? Date(JsonElement e, string name)
    {
        if (Str(e, name) is not { } text) return null;
        if (!DateOnly.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateOnly date)) return null;
        return new DateTimeOffset(date.ToDateTime(TimeOnly.MinValue) + ResetTime, ServerOffset);
    }

    // ── 逆境深塔 ──────────────────────────────────────────

    /// <summary><c>{season: {area: {floor: {cost: [层]}}}}</c>。</summary>
    public static TowerDetail ParseTower(int season, string json)
    {
        using JsonDocument doc = JsonDocument.Parse(json);
        JsonElement root = doc.RootElement;
        var areas = new List<TowerArea>();
        if (root.ValueKind != JsonValueKind.Object) return new TowerDetail(season, areas);
        JsonElement seasonNode = root.TryGetProperty(season.ToString(CultureInfo.InvariantCulture), out JsonElement exact)
            ? exact
            : root.EnumerateObject().Select(p => p.Value).FirstOrDefault();
        if (seasonNode.ValueKind != JsonValueKind.Object) return new TowerDetail(season, areas);

        foreach (JsonProperty area in seasonNode.EnumerateObject().OrderBy(p => NumberKey(p.Name)))
        {
            if (area.Value.ValueKind != JsonValueKind.Object) continue;
            var floors = new List<TowerFloor>();
            string? areaName = null;
            foreach (JsonProperty floor in area.Value.EnumerateObject().OrderBy(p => NumberKey(p.Name)))
            {
                if (floor.Value.ValueKind != JsonValueKind.Object) continue;
                foreach (JsonProperty cost in floor.Value.EnumerateObject())
                {
                    JsonElement item = Array(cost.Value).FirstOrDefault();
                    if (item.ValueKind != JsonValueKind.Object) continue;
                    areaName ??= Str(item, "areaName");
                    var buffs = Array(Prop(item, "buffs")).Select(b => Str(b, "desc")).OfType<string>().Where(d => d.Length > 0).ToList();
                    var targets = Array(Prop(item, "targets"))
                        .Select(t => new TowerTarget(Int(t, "score") ?? 0, Str(t, "desc") ?? string.Empty))
                        .Where(t => t.Text.Length > 0)
                        .ToList();
                    floors.Add(new TowerFloor(Int(item, "floor") ?? NumberKey(floor.Name), Int(item, "cost") ?? NumberKey(cost.Name),
                        Monsters(Prop(item, "monsters")), buffs, targets));
                    break;
                }
            }
            if (floors.Count > 0) areas.Add(new TowerArea(NumberKey(area.Name), areaName ?? area.Name, floors));
        }
        return new TowerDetail(season, areas);
    }

    // ── 冥歌海墟 ──────────────────────────────────────────

    public static WastesDetail ParseWastes(int season, string json)
    {
        using JsonDocument doc = JsonDocument.Parse(json);
        JsonElement root = doc.RootElement;
        var groups = new List<WastesGroup>();
        var tokens = new List<WastesToken>();
        if (root.ValueKind != JsonValueKind.Object) return new WastesDetail(season, groups, tokens);

        foreach (JsonElement g in Array(Prop(root, "stageGroups")).OrderBy(g => Int(g, "outIndex") ?? 0))
        {
            var levels = Array(Prop(g, "levels")).OrderBy(l => Int(l, "orderIndex") ?? 0).Select(WastesLevelOf).OfType<WastesLevel>().ToList();
            if (levels.Count > 0) groups.Add(new WastesGroup(Str(g, "name") ?? string.Empty, levels));
        }
        // 没有分组时退回顶层 levels（内容与分组里的相同）。
        if (groups.Count == 0)
        {
            var levels = Array(Prop(root, "levels")).OrderBy(l => Int(l, "orderIndex") ?? 0).Select(WastesLevelOf).OfType<WastesLevel>().ToList();
            if (levels.Count > 0) groups.Add(new WastesGroup(string.Empty, levels));
        }
        foreach (JsonElement b in Array(Prop(root, "buffItems")).OrderBy(b => Int(b, "orderIndex") ?? 0))
        {
            JsonElement item = Prop(b, "item");
            if (Str(item, "name") is not { Length: > 0 } name) continue;
            tokens.Add(new WastesToken(name, Str(item, "iconMiddle") ?? Str(item, "icon"), Int(item, "qualityId") ?? 0));
        }
        return new WastesDetail(season, groups, tokens);
    }

    private static WastesLevel? WastesLevelOf(JsonElement l)
    {
        if (Int(l, "id") is not { } id) return null;
        var scores = Array(Prop(l, "targetScore")).Select(e => e.ValueKind == JsonValueKind.Number && e.TryGetInt32(out int v) ? v : (int?)null).ToList();
        var stages = Array(Prop(l, "scoreStage")).Select(e => e.ValueKind == JsonValueKind.String ? e.GetString() : null).ToList();
        var grades = new List<WastesGrade>();
        for (int i = 0; i < Math.Min(scores.Count, stages.Count); i++)
        {
            if (scores[i] is { } score && stages[i] is { Length: > 0 } grade) grades.Add(new WastesGrade(grade, score));
        }
        var parts = Array(Prop(l, "stages"))
            .Select(s => new WastesStage(Str(s, "dungeonDesc") ?? string.Empty, Monsters(Prop(s, "monsters"))))
            .ToList();
        return new WastesLevel(id, Str(l, "title") ?? string.Empty, Str(l, "desc") ?? string.Empty, Bool(l, "endLess"),
            Int(l, "passScore") ?? 0, grades, parts);
    }

    // ── 终焉矩阵 ──────────────────────────────────────────

    public static MatrixDetail ParseMatrix(int season, string json)
    {
        using JsonDocument doc = JsonDocument.Parse(json);
        JsonElement root = doc.RootElement;
        var modes = new List<MatrixMode>();
        var roles = new List<MatrixRole>();
        if (root.ValueKind != JsonValueKind.Object) return new MatrixDetail(season, string.Empty, null, null, modes, roles);

        foreach (JsonElement level in Array(Prop(root, "Levels")).OrderBy(l => Int(l, "Diff") ?? 0))
        {
            var buffs = Array(Prop(level, "NewTowerBuffs"))
                .Select(b => new MatrixBuff(Str(b, "Name") ?? string.Empty, Str(b, "Desc") ?? string.Empty, Str(b, "Icon")))
                .Where(b => b.Name.Length > 0 || b.Description.Length > 0)
                .ToList();
            var thresholds = Array(Prop(level, "ScoreLevelRule"))
                .OrderBy(r => Int(r, "Key") ?? 0)
                .Select(r => Int(r, "Value") ?? 0)
                .Where(v => v > 0)
                .ToList();
            var rounds = Array(Prop(level, "Waves"))
                .Where(w => Prop(w, "IsShowInView") is not { ValueKind: JsonValueKind.False })
                .GroupBy(w => Int(w, "Round") ?? 1)
                .OrderBy(g => g.Key)
                .Select(g => new MatrixRound(g.Key, g.Max(w => Int(w, "MonsterLevel") ?? 0),
                    g.OrderBy(w => Int(w, "Wave") ?? 0).Select(MatrixEnemyOf).ToList()))
                .ToList();
            modes.Add(new MatrixMode(Int(level, "Id") ?? 0, Str(level, "Name") ?? string.Empty, Int(level, "Diff") ?? 0,
                Int(level, "TeamLimit") ?? 0, thresholds, buffs, rounds));
        }
        foreach (JsonElement r in Array(Prop(root, "Roles")))
        {
            JsonElement info = Prop(r, "RoleInfo");
            if (Str(info, "Name") is not { Length: > 0 } name) continue;
            string desc = string.Join("\n", Array(Prop(r, "EnhanceSkillDesc")).Select(d => Str(d, "Value")).OfType<string>().Where(d => d.Length > 0));
            roles.Add(new MatrixRole(Int(r, "Id") ?? 0, name, Str(info, "RoleHeadIconCircle") ?? Str(info, "RoleHeadIcon"),
                Int(info, "QualityId") ?? 0, Int(info, "ElementId") ?? 0, desc));
        }
        return new MatrixDetail(Int(root, "Season") ?? season, Str(root, "Name") ?? string.Empty, NullIfEmpty(Str(root, "SeasonName")),
            NullIfEmpty(Str(root, "EndVersion")), modes, roles);
    }

    private static MatrixEnemy MatrixEnemyOf(JsonElement w)
    {
        var tags = Array(Prop(w, "Tags")).Select(t => Str(t, "Name")).OfType<string>().Where(t => t.Length > 0).ToList();
        var skills = Array(Prop(w, "ShowBuffIds"))
            .Select(b => new MatrixSkill(Str(b, "SkillTitle") ?? string.Empty, Str(b, "SkillDesc") ?? string.Empty))
            .Where(s => s.Title.Length > 0 || s.Description.Length > 0)
            .ToList();
        var features = Array(Prop(w, "RecommendTeamFeature")).Select(f => Str(f, "Name")).OfType<string>().Where(f => f.Length > 0).ToList();
        string? icon = NullIfEmpty(Str(w, "SmallIconHandBook")) ?? NullIfEmpty(Str(w, "Icon"));
        return new MatrixEnemy(Long(w, "MonsterId") ?? 0, Str(w, "Name") ?? string.Empty, icon, Int(w, "ElementId") ?? -1,
            Int(w, "KillScore") ?? 0, tags, skills, features);
    }

    // ── 敌人 ──────────────────────────────────────────────

    private static IReadOnlyList<ChallengeMonster> Monsters(JsonElement list)
    {
        var result = new List<ChallengeMonster>();
        foreach (JsonElement m in Array(list))
        {
            if (Str(m, "name") is not { Length: > 0 } name) continue;
            var elements = Array(Prop(m, "elements"))
                .Select(e => Int(e, "id") is { } id ? new ChallengeElement(id, Str(e, "name") ?? string.Empty, Str(e, "icon")) : null)
                .OfType<ChallengeElement>()
                .ToList();
            if (elements.Count == 0 && Prop(m, "element") is { ValueKind: JsonValueKind.Object } single && Int(single, "id") is { } sid)
                elements.Add(new ChallengeElement(sid, Str(single, "name") ?? string.Empty, Str(single, "icon")));

            int? level = null;
            double? life = null;
            var resistances = new List<ChallengeResistance>();
            foreach (JsonElement p in Array(Prop(m, "whiteGreenProps")))
            {
                string? key = Str(p, "key");
                double? value = Prop(p, "value") is { ValueKind: JsonValueKind.Number } v ? v.GetDouble() : null;
                if (key is null || value is null) continue;
                if (key == "Lv") level = (int)value.Value;
                else if (key == "LifeMax") life = value;
                else if (key == "DamageResistancePhys") resistances.Add(new ChallengeResistance(0, Str(p, "name") ?? string.Empty, value.Value));
                else if (key.StartsWith("DamageResistanceElement", StringComparison.Ordinal)
                         && int.TryParse(key.AsSpan("DamageResistanceElement".Length), NumberStyles.Integer, CultureInfo.InvariantCulture, out int element))
                    resistances.Add(new ChallengeResistance(element, Str(p, "name") ?? string.Empty, value.Value));
            }
            resistances.Sort((a, b) => a.ElementId.CompareTo(b.ElementId));
            result.Add(new ChallengeMonster(Long(m, "id") ?? 0, name, Str(m, "icon"), elements, level, life, resistances, Str(Prop(m, "rarity"), "desc")));
        }
        return result;
    }

    // ── JSON 小工具 ───────────────────────────────────────

    private static JsonElement Prop(JsonElement e, string name) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out JsonElement v) ? v : default;

    private static IEnumerable<JsonElement> Array(JsonElement e) =>
        e.ValueKind == JsonValueKind.Array ? e.EnumerateArray() : [];

    private static string? Str(JsonElement e, string name) =>
        Prop(e, name) is { ValueKind: JsonValueKind.String } v ? v.GetString() : null;

    private static int? Int(JsonElement e, string name) => Prop(e, name) switch
    {
        { ValueKind: JsonValueKind.Number } v when v.TryGetInt32(out int i) => i,
        { ValueKind: JsonValueKind.String } v when int.TryParse(v.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int i) => i,
        _ => null,
    };

    private static long? Long(JsonElement e, string name) => Prop(e, name) switch
    {
        { ValueKind: JsonValueKind.Number } v when v.TryGetInt64(out long i) => i,
        { ValueKind: JsonValueKind.String } v when long.TryParse(v.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out long i) => i,
        _ => null,
    };

    private static bool Bool(JsonElement e, string name) => Prop(e, name).ValueKind == JsonValueKind.True;

    private static int NumberKey(string key) =>
        int.TryParse(key, NumberStyles.Integer, CultureInfo.InvariantCulture, out int n) ? n : int.MaxValue;

    private static string? NullIfEmpty(string? s) => string.IsNullOrWhiteSpace(s) ? null : s;
}
