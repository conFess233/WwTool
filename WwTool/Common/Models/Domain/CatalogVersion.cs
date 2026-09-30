using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace WwTool.Common.Models.Domain;

/// <summary>资料版本按数字段比较，兼容旧整数版本；缺少的末尾段视为零。</summary>
[JsonConverter(typeof(CatalogVersionJsonConverter))]
public readonly struct CatalogVersion : IComparable<CatalogVersion>, IEquatable<CatalogVersion>
{
    private readonly string? text;
    private CatalogVersion(string text) => this.text = text;

    /// <summary>接受一至四段非负整数，版本整体必须大于零。</summary>
    public static CatalogVersion Parse(string value)
    {
        string[] parts = value.Split('.');
        if (parts.Length is < 1 or > 4 || parts.Any(x => !long.TryParse(x, NumberStyles.None,
                CultureInfo.InvariantCulture, out long segment) || segment < 0) ||
            parts.All(x => long.Parse(x, CultureInfo.InvariantCulture) == 0))
            throw new FormatException("Catalog version must contain one to four non-negative integer segments and be greater than zero.");
        return new CatalogVersion(value);
    }

    /// <summary>逐段比较，避免字符串排序和浮点小数丢失版本含义。</summary>
    public int CompareTo(CatalogVersion other)
    {
        string[] left = ToString().Split('.'), right = other.ToString().Split('.');
        for (int i = 0; i < Math.Max(left.Length, right.Length); i++)
        {
            long a = i < left.Length ? long.Parse(left[i], CultureInfo.InvariantCulture) : 0;
            long b = i < right.Length ? long.Parse(right[i], CultureInfo.InvariantCulture) : 0;
            int comparison = a.CompareTo(b);
            if (comparison != 0) return comparison;
        }
        return 0;
    }

    public override string ToString() => text ?? "0";
    public bool Equals(CatalogVersion other) => CompareTo(other) == 0;
    public override bool Equals(object? obj) => obj is CatalogVersion other && Equals(other);
    public override int GetHashCode()
    {
        var segments = ToString().Split('.').Select(x => long.Parse(x, CultureInfo.InvariantCulture)).ToList();
        while (segments.Count > 1 && segments[^1] == 0) segments.RemoveAt(segments.Count - 1);
        var hash = new HashCode();
        foreach (long segment in segments) hash.Add(segment);
        return hash.ToHashCode();
    }
    public static bool operator >(CatalogVersion left, CatalogVersion right) => left.CompareTo(right) > 0;
    public static bool operator <(CatalogVersion left, CatalogVersion right) => left.CompareTo(right) < 0;
}

/// <summary>读取旧整数或分段版本字符串，写入时始终使用字符串。</summary>
public sealed class CatalogVersionJsonConverter : JsonConverter<CatalogVersion>
{
    public override CatalogVersion Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        string? value = reader.TokenType switch
        {
            JsonTokenType.String => reader.GetString(),
            JsonTokenType.Number when reader.TryGetInt64(out long integer) => integer.ToString(CultureInfo.InvariantCulture),
            _ => null
        };
        if (value is null) throw new JsonException("Catalog version must be a version string or a legacy integer.");
        try { return CatalogVersion.Parse(value); }
        catch (FormatException ex) { throw new JsonException("Invalid catalog version.", ex); }
    }

    public override void Write(Utf8JsonWriter writer, CatalogVersion value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.ToString());
}
