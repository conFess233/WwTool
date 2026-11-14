using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace WwTool.Web.Auth;

/// <summary>
/// 站点密码与恢复码的哈希。密码用 PBKDF2-SHA256（60 万次迭代、16 字节随机盐），格式
/// <c>pbkdf2-sha256$迭代次数$盐$哈希</c>，以后调高迭代次数时旧哈希仍可验证。
/// 恢复码本身是 100 位随机数，只需 SHA-256。
/// </summary>
public static class PasswordHasher
{
    private const int Iterations = 600_000;
    private const int SaltBytes = 16;
    private const int KeyBytes = 32;
    private const string Prefix = "pbkdf2-sha256";

    /// <summary>用户不存在时也做一次同样耗时的计算，避免通过响应时间探测用户名。</summary>
    private static readonly string DummyHash = Hash("wwtool-dummy-password");

    public static string Hash(string password)
    {
        byte[] salt = RandomNumberGenerator.GetBytes(SaltBytes);
        byte[] key = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, KeyBytes);
        return string.Create(CultureInfo.InvariantCulture, $"{Prefix}${Iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(key)}");
    }

    public static bool Verify(string password, string? stored)
    {
        string[] parts = (stored ?? DummyHash).Split('$');
        if (parts.Length != 4 || parts[0] != Prefix || !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out int iterations))
            return false;
        try
        {
            byte[] salt = Convert.FromBase64String(parts[2]);
            byte[] expected = Convert.FromBase64String(parts[3]);
            byte[] actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, expected.Length);
            return stored is not null && CryptographicOperations.FixedTimeEquals(actual, expected);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    /// <summary>新的恢复码：20 个 Crockford Base32 字符，按 4 位一组显示，例如 <c>7KQ2-M9XA-…</c>。</summary>
    public static string NewRecoveryCode()
    {
        const string alphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";
        Span<byte> bytes = stackalloc byte[20];
        RandomNumberGenerator.Fill(bytes);
        var sb = new StringBuilder(24);
        for (int i = 0; i < bytes.Length; i++)
        {
            if (i > 0 && i % 4 == 0) sb.Append('-');
            sb.Append(alphabet[bytes[i] & 31]);
        }
        return sb.ToString();
    }

    public static string HashRecoveryCode(string code) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(NormalizeRecoveryCode(code))));

    public static bool VerifyRecoveryCode(string code, string? stored) =>
        stored is not null && CryptographicOperations.FixedTimeEquals(
            Encoding.ASCII.GetBytes(HashRecoveryCode(code)), Encoding.ASCII.GetBytes(stored));

    /// <summary>忽略大小写、空格与连字符，并把容易看错的 O/I/L 当作 0/1/1。</summary>
    private static string NormalizeRecoveryCode(string code)
    {
        var sb = new StringBuilder(code.Length);
        foreach (char c in code.ToUpperInvariant())
        {
            if (c is '-' or ' ') continue;
            sb.Append(c switch { 'O' => '0', 'I' or 'L' => '1', _ => c });
        }
        return sb.ToString();
    }
}
