using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace WwTool.Common.Utils
{
    public class CheckPathExists
    {
        /// <summary>
        /// 确认指定的根目录或其直接下一级子目录下，是否存在指定的文件或路径
        /// </summary>
        /// <param name="rootDirectory">要检索的根目录</param>
        /// <param name="targetPath">要确认的目标文件名或相对路径</param>
        /// <returns>若存在目标文件或路径则返回 true，否则返回 false</returns>
        public static bool CheckPathExistsWithSubLevel(string rootDirectory, string targetPath)
        {
            // 如果根目录为空或不存在，直接返回 false
            if (string.IsNullOrWhiteSpace(rootDirectory) || !Directory.Exists(rootDirectory))
            {
                return false;
            }
            // 检查根目录下是否直接存在目标文件/路径
            string directPath = Path.Combine(rootDirectory, targetPath);
            if (File.Exists(directPath) || Directory.Exists(directPath))
            {
                return true;
            }
            // 检查直接下一级的子目录下是否包含目标文件/路径
            try
            {
                // 仅获取当前目录下的直接子目录
                string[] subDirectories = Directory.GetDirectories(rootDirectory, "*", SearchOption.TopDirectoryOnly);
                foreach (string subDir in subDirectories)
                {
                    string combinedSubPath = Path.Combine(subDir, targetPath);
                    if (File.Exists(combinedSubPath) || Directory.Exists(combinedSubPath))
                    {
                        return true;
                    }
                }
            }
            catch (UnauthorizedAccessException)
            {
                // 忽略可能存在的无访问权限子目录异常
            }
            catch (Exception)
            {
                // 忽略其他潜在异常以防止程序崩溃
            }
            return false;
        }
    }
}
