using System.Globalization;
using System.Text.Json;
using WwTool.Common.Models.Domain;
using WwTool.Common.Utils;

namespace WwTool.Services.Presentation;

/// <summary>从本地详情快照提取实际数据，绝不读取攻略推荐配置。</summary>
public static class GuideRoleHoverMapper
{
    /// <summary>兼容旧快照缺少属性名称的情况，并独立保留已知装备摘要。</summary>
    public static GuideRoleHoverDetails Map(string? json, GuideWeaponData? equipped,
        GameDataService catalog, string language, Action<JsonException> onInvalidJson)
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
                foreach (JsonElement item in items.EnumerateArray())
                {
                    string? value = Scalar(Child(item, "currentAmount"));
                    if (string.IsNullOrWhiteSpace(value)) continue;
                    string id = Scalar(Child(item, "gbId")) ?? string.Empty;
                    string name = AttributeName(id) ?? TextName(item, language)
                        ?? string.Format(CultureInfo.CurrentCulture, LanguageManager.Instance["Guide_AttributeUnknown"], id);
                    attributes.Add(new(name, value));
                }
            JsonElement current = Child(Child(root, "weapon"), "current");
            string? weaponId = Scalar(Child(current, "gbId"));
            if (!string.IsNullOrWhiteSpace(weaponId))
            {
                var fields = new List<GuideHoverAttribute>();
                // 只接受明确标为 current 的实际字段，不推算等级或谐振阶数。
                foreach (var (field, key) in new[] {
                    ("currentLevel", "Guide_WeaponLevel"),
                    ("currentBreakthrough", "Guide_WeaponBreakthrough"),
                    ("currentResonanceRank", "Guide_WeaponResonanceRank") })
                    if (Scalar(Child(current, field)) is { Length: > 0 } value)
                        fields.Add(new(LanguageManager.Instance[key], value));
                int star = int.TryParse(Scalar(Child(current, "star")), out int parsed) ? parsed : equipped?.Star ?? 0;
                string name = ItemName(weaponId, catalog, language);
                if (name == weaponId) name = TextName(current, language) ?? name;
                weapon = new(name, $"Local/Icons/{weaponId}.png", star, fields);
            }
        }
        catch (JsonException ex) { onInvalidJson(ex); }
        return new(attributes, weapon);
    }

    /// <summary>旧快照丢弃了 texts；已核对的属性 ID 使用三语资源回退。</summary>
    private static string? AttributeName(string id)
    {
        string? key = id switch {
            "2-1" => "Guide_AttributeHealth", "7-1" => "Guide_AttributeAttack",
            "10-1" => "Guide_AttributeDefense", "8-2" => "Guide_AttributeCritRate",
            "9-2" => "Guide_AttributeCritDamage", "11-2" => "Guide_AttributeEnergyRegen", _ => null };
        return key is null ? null : LanguageManager.Instance[key];
    }

    /// <summary>优先选择当前语言名称，未知资源保留稳定 ID。</summary>
    private static string ItemName(string id, GameDataService catalog, string language) =>
        int.TryParse(id, out int resourceId) ? catalog.GetItemById(resourceId)?.GetName(language) ?? id : id;

    /// <summary>只访问对象属性，容忍缺少或空的局部数据。</summary>
    private static JsonElement Child(JsonElement value, string name) =>
        value.ValueKind == JsonValueKind.Object && value.TryGetProperty(name, out var child) ? child : default;

    /// <summary>保留百分比和零值，排除对象、布尔值及空值。</summary>
    private static string? Scalar(JsonElement value) => value.ValueKind switch {
        JsonValueKind.String => value.GetString(), JsonValueKind.Number => value.GetRawText(), _ => null };

    /// <summary>名称由快照提供时使用精确语言匹配。</summary>
    private static string? TextName(JsonElement value, string language)
    {
        JsonElement texts = Child(value, "texts");
        if (texts.ValueKind != JsonValueKind.Array) return null;
        foreach (JsonElement text in texts.EnumerateArray())
            if (string.Equals(Scalar(Child(text, "language")), language, StringComparison.OrdinalIgnoreCase))
                return Scalar(Child(text, "name"));
        return null;
    }
}
