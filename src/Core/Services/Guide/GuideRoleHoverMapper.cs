using System.Globalization;
using System.Text.Json;
using WwTool.Core.Models.Domain;

namespace WwTool.Core.Services.Guide;

/// <summary>从本地攻略站详情快照提取实际数据（属性、当前装备武器），绝不读取攻略推荐配置。</summary>
public static class GuideRoleHoverMapper
{
    /// <summary>兼容旧快照缺少属性名称的情况，并独立保留已知装备摘要。</summary>
    /// <param name="text">按键取界面文案。</param>
    public static GuideRoleHoverDetails Map(string? json, GuideWeaponData? equipped,
        GameDataService catalog, string language, Func<string, string> text, Action<JsonException> onInvalidJson)
    {
        var attributes = new List<GuideHoverAttribute>();
        GuideHoverWeapon? weapon = equipped is null ? null : new(
            ItemName(equipped.WeaponGbId, catalog, language),
            $"Local/Icons/{equipped.WeaponGbId}.png", equipped.Star, []);
        if (string.IsNullOrWhiteSpace(json)) return new(attributes, weapon);
        try
        {
            using var document = JsonDocument.Parse(json);
            JsonElement root = document.RootElement;
            JsonElement items = Child(Child(root, "roleAttribute"), "items");
            if (items.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement item in items.EnumerateArray())
                {
                    string? value = Scalar(Child(item, "currentAmount"));
                    if (string.IsNullOrWhiteSpace(value)) continue;
                    string id = Scalar(Child(item, "gbId")) ?? string.Empty;
                    string name = AttributeName(id, text) ?? TextName(item, language)
                        ?? string.Format(CultureInfo.CurrentCulture, text("Guide_AttributeUnknown"), id);
                    attributes.Add(new(name, value));
                }
            }
            JsonElement current = Child(Child(root, "weapon"), "current");
            string? weaponId = Scalar(Child(current, "gbId"));
            if (!string.IsNullOrWhiteSpace(weaponId))
            {
                var fields = new List<GuideHoverAttribute>();
                // 只接受明确标为 current 的实际字段，不推算等级或谐振阶数。
                foreach (var (field, key) in new[]
                {
                    ("currentLevel", "Guide_WeaponLevel"),
                    ("currentBreakthrough", "Guide_WeaponBreakthrough"),
                    ("currentResonanceRank", "Guide_WeaponResonanceRank"),
                })
                {
                    if (Scalar(Child(current, field)) is { Length: > 0 } value) fields.Add(new(text(key), value));
                }
                int star = int.TryParse(Scalar(Child(current, "star")), out int parsed) ? parsed : equipped?.Star ?? 0;
                string name = ItemName(weaponId, catalog, language);
                if (name == weaponId) name = TextName(current, language) ?? name;
                weapon = new(name, $"Local/Icons/{weaponId}.png", star, fields);
            }
        }
        catch (JsonException ex)
        {
            onInvalidJson(ex);
        }
        return new(attributes, weapon);
    }

    /// <summary>
    /// 「我的养成」用的技能等级与当前声骸：只读 <c>roleSkill.addPointTarget[].currentLevel</c> 与
    /// <c>echo.current</c>，不读推荐方案（<c>main</c> / <c>spare</c> / <c>recommendLevel</c>）。
    /// 名称按 <paramref name="language"/> 取，快照里没有该语言时取任一语言（快照按同步时的界面语言保存）。
    /// </summary>
    public static GuideRoleBuild MapBuild(string? json, string language, Action<JsonException> onInvalidJson)
    {
        if (string.IsNullOrWhiteSpace(json)) return GuideRoleBuild.Empty;
        try
        {
            using var document = JsonDocument.Parse(json);
            JsonElement root = document.RootElement;
            var skills = new List<GuideSkillLevel>();
            JsonElement targets = Child(Child(root, "roleSkill"), "addPointTarget");
            if (targets.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement item in targets.EnumerateArray())
                {
                    if (Child(item, "currentLevel") is { ValueKind: JsonValueKind.Number } level && level.TryGetInt32(out int value))
                        skills.Add(new(HttpUrl(Scalar(Child(item, "pictureUrl"))), value, AnyName(item, language), AnyName(Child(item, "skillType"), language)));
                }
            }

            var echoes = new List<GuideEchoSlot>();
            var sets = new List<GuideEchoSet>();
            JsonElement current = Child(Child(root, "echo"), "current");
            JsonElement slots = Child(current, "echoAttributes");
            if (slots.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement slot in slots.EnumerateArray())
                {
                    if (Child(slot, "cost") is not { ValueKind: JsonValueKind.Number } cost || !cost.TryGetInt32(out int c)) continue;
                    int? level = Child(slot, "currentLevel") is { ValueKind: JsonValueKind.Number } l && l.TryGetInt32(out int lv) ? lv : null;
                    echoes.Add(new(c, level, AnyName(Child(slot, "attribute"), language), AnyName(Child(slot, "attribute2"), language)));
                }
            }
            JsonElement effects = Child(current, "echoSetEffects");
            if (effects.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement effect in effects.EnumerateArray())
                {
                    var set = new GuideEchoSet(HttpUrl(Scalar(Child(effect, "pictureUrl"))), AnyName(effect, language));
                    if ((set.ImageUrl ?? set.Name) is not null && !sets.Contains(set)) sets.Add(set);
                }
            }
            return new(skills, echoes, sets, AnyName(Child(current, "echoProps"), language));
        }
        catch (JsonException ex)
        {
            onInvalidJson(ex);
            return GuideRoleBuild.Empty;
        }
    }

    /// <summary><c>texts[]</c> 里当前语言的名称；没有时取第一个非空名称。</summary>
    private static string? AnyName(JsonElement value, string language)
    {
        if (TextName(value, language) is { Length: > 0 } name) return name;
        JsonElement texts = Child(value, "texts");
        if (texts.ValueKind != JsonValueKind.Array) return null;
        foreach (JsonElement item in texts.EnumerateArray())
        {
            if (Scalar(Child(item, "name")) is { Length: > 0 } any) return any;
        }
        return null;
    }

    private static string? HttpUrl(string? url) =>
        url is not null && Uri.TryCreate(url, UriKind.Absolute, out Uri? uri) && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp) ? url : null;

    /// <summary>
    /// 共鸣链数：优先用快照字段，缺失时由详情中已获得的共鸣链条目推算，都没有时按 0。
    /// </summary>
    public static int ResolveSequence(GuideRoleData role, Action<string, Exception?> warn)
    {
        if (role.Sequence is >= 0 and <= 6) return role.Sequence;
        if (!string.IsNullOrWhiteSpace(role.DetailJson))
        {
            try
            {
                using var document = JsonDocument.Parse(role.DetailJson);
                JsonElement items = Child(Child(document.RootElement, "roleResonance"), "items");
                if (items.ValueKind == JsonValueKind.Array)
                {
                    int acquired = items.EnumerateArray().Count(x =>
                        Child(x, "isAcquired") is { ValueKind: JsonValueKind.True });
                    return Math.Clamp(acquired, 0, 6);
                }
            }
            catch (JsonException ex)
            {
                warn($"无法从本地详情恢复角色 {role.RoleGbId} 的共鸣链。", ex);
            }
        }
        warn($"攻略站角色 {role.RoleGbId} 缺少有效共鸣链数据，按零链显示。", null);
        return 0;
    }

    // 旧快照丢弃了 texts；已核对的属性 ID 使用三语资源回退。
    private static string? AttributeName(string id, Func<string, string> text)
    {
        string? key = id switch
        {
            "2-1" => "Guide_AttributeHealth",
            "7-1" => "Guide_AttributeAttack",
            "10-1" => "Guide_AttributeDefense",
            "8-2" => "Guide_AttributeCritRate",
            "9-2" => "Guide_AttributeCritDamage",
            "11-2" => "Guide_AttributeEnergyRegen",
            _ => null,
        };
        return key is null ? null : text(key);
    }

    // 优先选择当前语言名称，未知资源保留稳定 ID。
    private static string ItemName(string id, GameDataService catalog, string language) =>
        int.TryParse(id, out int resourceId) ? catalog.GetItemById(resourceId)?.GetName(language) ?? id : id;

    private static JsonElement Child(JsonElement value, string name) =>
        value.ValueKind == JsonValueKind.Object && value.TryGetProperty(name, out var child) ? child : default;

    // 保留百分比和零值，排除对象、布尔值及空值。
    private static string? Scalar(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.String => value.GetString(),
        JsonValueKind.Number => value.GetRawText(),
        _ => null,
    };

    private static string? TextName(JsonElement value, string language)
    {
        JsonElement texts = Child(value, "texts");
        if (texts.ValueKind != JsonValueKind.Array) return null;
        foreach (JsonElement item in texts.EnumerateArray())
        {
            if (string.Equals(Scalar(Child(item, "language")), language, StringComparison.OrdinalIgnoreCase))
                return Scalar(Child(item, "name"));
        }
        return null;
    }
}
