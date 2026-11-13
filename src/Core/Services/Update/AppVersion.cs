using System.Globalization;

namespace WwTool.Core.Services.Update;

/// <summary>
/// 程序版本号：<c>v2.0.0</c>、<c>2.0.0</c>、<c>v2.0.0-dev</c>、<c>2.1</c> 等。按数字段比较，缺的段视为 0；
/// 带后缀（<c>-dev</c>、<c>-beta.1</c>）的是开发 / 预发布版本，同号时低于正式版。
/// </summary>
public readonly record struct AppVersion(int Major, int Minor, int Patch, string Suffix) : IComparable<AppVersion>
{
    public bool IsPrerelease => Suffix.Length > 0;

    public static bool TryParse(string? text, out AppVersion version)
    {
        version = default;
        if (string.IsNullOrWhiteSpace(text)) return false;
        string s = text.Trim();
        if (s.StartsWith('v') || s.StartsWith('V')) s = s[1..];
        // 构建元数据（+commit）不参与比较。
        int plus = s.IndexOf('+');
        if (plus >= 0) s = s[..plus];
        string suffix = string.Empty;
        int dash = s.IndexOf('-');
        if (dash >= 0)
        {
            suffix = s[(dash + 1)..];
            s = s[..dash];
            if (suffix.Length == 0) return false;
        }
        string[] parts = s.Split('.');
        if (parts.Length is < 1 or > 3) return false;
        var numbers = new int[3];
        for (int i = 0; i < parts.Length; i++)
        {
            if (!int.TryParse(parts[i], NumberStyles.None, CultureInfo.InvariantCulture, out numbers[i])) return false;
        }
        version = new AppVersion(numbers[0], numbers[1], numbers[2], suffix);
        return true;
    }

    public int CompareTo(AppVersion other)
    {
        int c = Major.CompareTo(other.Major);
        if (c != 0) return c;
        c = Minor.CompareTo(other.Minor);
        if (c != 0) return c;
        c = Patch.CompareTo(other.Patch);
        if (c != 0) return c;
        // 同号：正式版高于带后缀的版本；两个都带后缀时按文字比较。
        if (IsPrerelease != other.IsPrerelease) return IsPrerelease ? -1 : 1;
        return string.CompareOrdinal(Suffix, other.Suffix);
    }

    public static bool operator >(AppVersion a, AppVersion b) => a.CompareTo(b) > 0;
    public static bool operator <(AppVersion a, AppVersion b) => a.CompareTo(b) < 0;
    public static bool operator >=(AppVersion a, AppVersion b) => a.CompareTo(b) >= 0;
    public static bool operator <=(AppVersion a, AppVersion b) => a.CompareTo(b) <= 0;

    /// <summary>不带 v 前缀的数字形式（<c>2.0.0</c>），与发布包文件名一致。</summary>
    public string Number => $"{Major}.{Minor}.{Patch}";

    public override string ToString() => IsPrerelease ? $"v{Number}-{Suffix}" : $"v{Number}";
}
