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
        /// <returns>包含文件每一行的可枚举集合</returns>
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
    }
}
