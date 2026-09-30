using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using WwTool.Common.Models.Domain;

namespace WwTool.Services;

/// <summary>四类资料的校验、无损合并与原子文件写入。</summary>
public static class CatalogJson
{
    public static readonly string[] Categories = ["Characters", "Weapons", "Albums", "Motors"];
    public static readonly JsonSerializerOptions Options = new() { WriteIndented = true, PropertyNameCaseInsensitive = true };

    public static JsonObject Validate(JsonObject document, string category)
    {
        if (!Categories.Contains(category) ||
            document["items"] is not JsonObject items || items.Count == 0)
            throw new InvalidDataException("Invalid catalog header.");
        _ = ReadVersion(document);
        foreach (var (id, node) in items)
        {
            if (!int.TryParse(id, out int numeric) || numeric <= 0 || node is not JsonObject item)
                throw new InvalidDataException("Invalid catalog ID.");
            string? type = item["type"]?.GetValue<string>();
            bool typeMatches = category switch
            {
                "Characters" => type == "Character",
                "Weapons" => type == "Weapon",
                "Albums" => type == "Album",
                "Motors" => type is "MotorSkin" or "MotorSticker" or "MotorFrame" or "MotorDecoration",
                _ => false
            };
            if (!typeMatches || item["names"] is not JsonObject)
                throw new InvalidDataException("Invalid catalog item.");
            // 尚无任何名称的记录保留原始字段，暂不进入展示模型；后续同步可以补全。
            if (!HasDisplayName(item)) continue;
            if (category is "Characters" or "Weapons" && item["qualityLevel"]?.GetValue<int>() is not (>= 1 and <= 5))
                throw new InvalidDataException("Invalid rarity.");
            foreach (string key in new[] { "iconUrl", "portraitUrl" })
                if (item[key] is JsonValue url && !string.IsNullOrWhiteSpace(url.GetValue<string>()) && !IsImageUrl(url.GetValue<string>()))
                    throw new InvalidDataException("Invalid image URL.");
            if (item["isLimited"] is not null) _ = item["isLimited"]!.GetValue<bool>();
        }
        return document;
    }

    /// <summary>名称全空的未完成记录不展示，但仍保留在资料文档中。</summary>
    public static bool HasDisplayName(JsonObject item) => item["names"] is JsonObject names &&
        names.Any(x => !string.IsNullOrWhiteSpace(x.Value?.GetValue<string>()));

    /// <summary>目录和同步逻辑共用版本读取，格式错误进入保留旧资料的错误边界。</summary>
    public static CatalogVersion ReadVersion(JsonObject document)
    {
        if (document["version"] is not JsonValue value) throw new InvalidDataException("Missing catalog version.");
        try { return value.Deserialize<CatalogVersion>(Options); }
        catch (JsonException ex) { throw new InvalidDataException("Invalid catalog version; use a string such as 3.7.0.", ex); }
    }

    public static bool IsImageUrl(string? value) => Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
        uri.Scheme == Uri.UriSchemeHttps && string.IsNullOrEmpty(uri.UserInfo);

    /// <summary>仅覆盖有效字段；保留未知字段、本地额外条目及旧译名。</summary>
    public static JsonObject Merge(JsonObject local, JsonObject incoming)
    {
        var result = (JsonObject)local.DeepClone();
        MergeInto(result, incoming);
        return result;
    }

    private static void MergeInto(JsonObject target, JsonObject incoming)
    {
        foreach (var (key, value) in incoming)
        {
            if (value is null || value is JsonValue scalar && scalar.TryGetValue<string>(out var text) && string.IsNullOrWhiteSpace(text)) continue;
            if (value is JsonObject child && target[key] is JsonObject existing) MergeInto(existing, child);
            else if (value is not JsonArray array || array.Count > 0) target[key] = value.DeepClone();
        }
    }

    public static Task WriteAsync(string path, JsonObject document, CancellationToken token = default) =>
        WwTool.Common.Utils.AtomicFile.WriteAsync(path,
            (stream, cancellation) => JsonSerializer.SerializeAsync(stream, document, Options, cancellation), token);
}
