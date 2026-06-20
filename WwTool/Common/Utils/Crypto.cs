using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace WwTool.Common.Utils
{
    /// <summary>
    /// 加密工具类
    /// </summary>
    public class Crypto
    {
        /// <summary>
        /// 计算请求签名
        /// </summary>
        /// <param name="dict">请求头字典</param>
        /// <returns></returns>
        public static string GenerateSignature(Dictionary<string, string> dict, string secretKey)
        {
            // 排序键名（排除 sign 和 极验 geetest 参数，无论大小写）
            var sortedKeys = dict.Keys
                .Where(k => !string.Equals(k, "sign", StringComparison.OrdinalIgnoreCase)
                         && !k.StartsWith("geetest", StringComparison.OrdinalIgnoreCase))
                .OrderBy(k => k, StringComparer.Ordinal)
                .ToList();

            // 构造签名前的拼接字符串
            var sb = new StringBuilder();
            for (int i = 0; i < sortedKeys.Count; i++)
            {
                string key = sortedKeys[i];
                string val = dict[key];

                sb.Append(key).Append('=').Append(val);
                if (i < sortedKeys.Count - 1)
                {
                    sb.Append('&');
                }
            }

            // 拼接 SecretKey
            sb.Append(secretKey);

            // 计算 MD5
            byte[] inputBytes = Encoding.UTF8.GetBytes(sb.ToString());
            byte[] hashBytes = MD5.HashData(inputBytes);

            return Convert.ToHexString(hashBytes).ToLowerInvariant();
        }

        /// <summary>
        /// 生成设备号
        /// </summary>
        /// <returns>大写UUIDv4字符串</returns>
        public static string GetDeviceNum()
        {
            return Guid.NewGuid().ToString("D").ToUpperInvariant();
        }

        /// <summary>
        /// 混淆密码
        /// </summary>
        /// <param name="password"></param>
        /// <returns>混淆后的字符串</returns>
        public static string EncodePassword(string password)
        {
            if (string.IsNullOrEmpty(password))
            {
                return string.Empty;
            }

            // Base64 编码
            byte[] passwordBytes = Encoding.UTF8.GetBytes(password);
            string base64String = Convert.ToBase64String(passwordBytes);
            char[] chars = base64String.ToCharArray();

            // 邻位互换，4个字符一组，做两轮
            int n = chars.Length;
            foreach (int offset in new[] { 0, 1 })
            {
                int i = offset;
                while (i + 2 < n)
                {
                    // 交换位置i和i+2的字符
                    char temp = chars[i];
                    chars[i] = chars[i + 2];
                    chars[i + 2] = temp;

                    if (i + 6 >= n)
                    {
                        break;
                    }
                    i += 4;
                }
            }
            return new string(chars);
        }

        /// <summary>
        /// 使用 Windows DPAPI 加密数据，保证本地存储安全
        /// </summary>
        /// <param name="plainText">要加密的字符串</param>
        /// <returns>Base64字符串</returns>
        public static string Encrypt(string plainText)
        {
            if (string.IsNullOrEmpty(plainText)) return string.Empty;
            try
            {
                byte[] plainBytes = Encoding.UTF8.GetBytes(plainText);
                byte[] encryptedBytes = ProtectedData.Protect(plainBytes, null, DataProtectionScope.CurrentUser);
                return Convert.ToBase64String(encryptedBytes);
            }
            catch
            {
                return string.Empty;
            }
        }

        /// <summary>
        /// 使用 Windows DPAPI 解密数据
        /// </summary>
        /// <param name="cipherText">密文</param>
        /// <returns>明文</returns>
        public static string Decrypt(string cipherText)
        {
            if (string.IsNullOrEmpty(cipherText)) return string.Empty;
            try
            {
                byte[] cipherBytes = Convert.FromBase64String(cipherText);
                byte[] decryptedBytes = ProtectedData.Unprotect(cipherBytes, null, DataProtectionScope.CurrentUser);
                return Encoding.UTF8.GetString(decryptedBytes);
            }
            catch
            {
                return string.Empty;
            }
        }
    }
}
