using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;
using WwTool.Core.Abstractions.Platform;

namespace WwTool.Core.Platform;

/// <summary>
/// 带版本前缀的凭据加密。密文格式：
/// <list type="bullet">
/// <item><c>wwt1:dpapi:&lt;base64&gt;</c>：Windows DPAPI（CurrentUser）。</item>
/// <item><c>wwt1:aesgcm:&lt;base64(nonce|tag|cipher)&gt;</c>：AES-256-GCM，主密钥保存在权限 0600 的本地密钥文件。</item>
/// <item>无前缀：1.2.1 写入的 DPAPI 密文，只在 Windows 上可解。</item>
/// </list>
/// 注：计划中 macOS 钥匙串、Linux Secret Service 保存主密钥尚未接入，目前非 Windows 一律使用密钥文件
/// （即计划中的回退方案）。接入后新增 <c>wwt1:keychain</c> 等前缀即可，旧密文仍按本类解。
/// </summary>
public sealed class VersionedSecretProtector : ISecretProtector
{
    private const string Prefix = "wwt1:";
    private const string DpapiTag = Prefix + "dpapi:";
    private const string AesGcmTag = Prefix + "aesgcm:";
    private const int KeySize = 32;
    private const int NonceSize = 12;
    private const int TagSize = 16;

    private readonly string _keyFilePath;
    private readonly Lock _keyLock = new();
    private byte[]? _key;

    /// <param name="keyFilePath">非 Windows 系统的主密钥文件路径，通常为 <c>&lt;Config&gt;/secret.key</c>。</param>
    public VersionedSecretProtector(string keyFilePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(keyFilePath);
        _keyFilePath = keyFilePath;
    }

    public string Protect(string plainText)
    {
        ArgumentException.ThrowIfNullOrEmpty(plainText);
        byte[] plain = Encoding.UTF8.GetBytes(plainText);
        try
        {
            return OperatingSystem.IsWindows()
                ? DpapiTag + Convert.ToBase64String(DpapiProtect(plain))
                : AesGcmTag + Convert.ToBase64String(AesGcmProtect(plain));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plain);
        }
    }

    public bool TryUnprotect(string cipherText, out string plainText)
    {
        plainText = string.Empty;
        if (string.IsNullOrEmpty(cipherText)) return false;

        try
        {
            byte[]? plain;
            if (cipherText.StartsWith(AesGcmTag, StringComparison.Ordinal))
            {
                plain = AesGcmUnprotect(Convert.FromBase64String(cipherText[AesGcmTag.Length..]));
            }
            else
            {
                if (!OperatingSystem.IsWindows()) return false;
                string payload = cipherText.StartsWith(DpapiTag, StringComparison.Ordinal)
                    ? cipherText[DpapiTag.Length..]
                    : cipherText; // 1.2.1 无前缀密文
                plain = DpapiUnprotect(Convert.FromBase64String(payload));
            }

            if (plain is null) return false;
            plainText = Encoding.UTF8.GetString(plain);
            CryptographicOperations.ZeroMemory(plain);
            return true;
        }
        catch (Exception ex) when (ex is CryptographicException or FormatException)
        {
            // 密文来自别的电脑或系统用户、被篡改或已损坏：按“需要重新登录”处理。
            return false;
        }
    }

    [SupportedOSPlatform("windows")]
    private static byte[] DpapiProtect(byte[] plain) =>
        ProtectedData.Protect(plain, null, DataProtectionScope.CurrentUser);

    [SupportedOSPlatform("windows")]
    private static byte[] DpapiUnprotect(byte[] cipher) =>
        ProtectedData.Unprotect(cipher, null, DataProtectionScope.CurrentUser);

    private byte[] AesGcmProtect(byte[] plain)
    {
        byte[] key = GetOrCreateKey();
        byte[] output = new byte[NonceSize + TagSize + plain.Length];
        Span<byte> nonce = output.AsSpan(0, NonceSize);
        RandomNumberGenerator.Fill(nonce);
        using var aes = new AesGcm(key, TagSize);
        aes.Encrypt(nonce, plain, output.AsSpan(NonceSize + TagSize), output.AsSpan(NonceSize, TagSize));
        return output;
    }

    private byte[]? AesGcmUnprotect(byte[] data)
    {
        if (data.Length < NonceSize + TagSize) return null;
        byte[]? key = TryLoadKey();
        if (key is null) return null;
        byte[] plain = new byte[data.Length - NonceSize - TagSize];
        using var aes = new AesGcm(key, TagSize);
        aes.Decrypt(data.AsSpan(0, NonceSize), data.AsSpan(NonceSize + TagSize), data.AsSpan(NonceSize, TagSize), plain);
        return plain;
    }

    private byte[] GetOrCreateKey()
    {
        lock (_keyLock)
        {
            if (TryLoadKey() is { } existing) return existing;

            byte[] key = RandomNumberGenerator.GetBytes(KeySize);
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(_keyFilePath))!);
            var options = new FileStreamOptions { Mode = FileMode.CreateNew, Access = FileAccess.Write };
            if (!OperatingSystem.IsWindows())
            {
                // 创建时即为 0600，避免先以默认权限落盘再改权限的窗口期。
                options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
            }
            using (var stream = new FileStream(_keyFilePath, options))
            {
                stream.Write(key);
                stream.Flush(true);
            }
            _key = key;
            return key;
        }
    }

    private byte[]? TryLoadKey()
    {
        lock (_keyLock)
        {
            if (_key is not null) return _key;
            if (!File.Exists(_keyFilePath)) return null;
            byte[] key = File.ReadAllBytes(_keyFilePath);
            if (key.Length != KeySize) return null;
            _key = key;
            return key;
        }
    }
}
