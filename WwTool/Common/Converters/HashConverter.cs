using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace WwTool.Common.Converters
{
    /// <summary>
    /// 哈希转换器
    /// </summary>
    public static class HashConverter
    {
        public static string ToSHA256(string input)
        {
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
            return Convert.ToHexString(bytes).ToLower();
        }
    }
}
