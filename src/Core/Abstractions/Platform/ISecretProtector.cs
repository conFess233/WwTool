namespace WwTool.Core.Abstractions.Platform;

/// <summary>
/// 本地凭据（OauthCode、攻略令牌）的加解密。
/// 密文带版本前缀区分算法；无前缀的密文视为 1.2.1 写入的 Windows DPAPI 密文。
/// </summary>
public interface ISecretProtector
{
    /// <summary>加密明文，返回可直接存库的字符串。</summary>
    string Protect(string plainText);

    /// <summary>
    /// 尝试解密。密文损坏、来自另一台电脑或另一个系统用户时返回 <c>false</c>，
    /// 调用方应要求用户重新登录，而不是当作空凭据继续。
    /// </summary>
    bool TryUnprotect(string cipherText, out string plainText);
}
