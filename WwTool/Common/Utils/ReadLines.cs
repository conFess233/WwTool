using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace WwTool.Common.Utils
{
    /// <summary>
    /// 文件读取工具类
    /// </summary>
    public static class ReadLines
    {
        /// <summary>
        /// 以共享读写的方式逐行读取文本文件
        /// 适用于读取正在被其他进程（如游戏客户端）写入的日志文件
        /// </summary>
        /// <param name="path">文件绝对路径</param>
        /// <returns>日志行</returns>
        public static IEnumerable<string> ReadLinesShared(string path)
        {
            using FileStream fs =
                new FileStream(
                    path,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.ReadWrite);

            using StreamReader reader = new StreamReader(fs);

            while (!reader.EndOfStream)
            {
                yield return reader.ReadLine();
            }
        }

        /// <summary>
        /// 读取并解密加密的日志文件，以迭代器形式返回解密后的每一行文本
        /// </summary>
        /// <param name="path">加密的日志文件路径</param>
        /// <returns>解密后的明文日志行</returns>
        public static IEnumerable<string> ReadLinesDecrypt(string path)
        {
            // 检查文件是否存在，如果不存在则直接结束
            if (!File.Exists(path)) yield break;
            byte[] encryptedData;

            // 以共享读写方式打开文件流，防止文件被其他进程（如游戏）锁死而抛出异常
            using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                using (MemoryStream ms = new MemoryStream())
                {
                    fs.CopyTo(ms);
                    encryptedData = ms.ToArray();
                }
            }
            byte[] decryptedData = new byte[encryptedData.Length];

            // 遍历所有字节，根据字节的奇偶性进行对应的异或解密
            for (int i = 0; i < encryptedData.Length; i++)
            {
                byte b = encryptedData[i];
                // 奇数字节异或 0xA5，偶数字节异或 0xEF
                decryptedData[i] = (byte)((b & 1) != 0 ? b ^ 0xA5 : b ^ 0xEF);
            }

            // 将解密后的字节数组导入内存流，并使用 UTF-8 编码逐行读取返回
            using var decryptedMs = new MemoryStream(decryptedData);
            using var reader = new StreamReader(decryptedMs, Encoding.UTF8);
            while (!reader.EndOfStream)
            {
                yield return reader.ReadLine();
            }
        }
    }
}
