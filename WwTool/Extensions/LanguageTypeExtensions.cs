using System;
using System.Collections.Generic;
using System.Text;
using WwTool.Common.Enums;

namespace WwTool.Extensions
{
    /// <summary>
    /// 语言类型扩展方法
    /// </summary>
    public static class LanguageTypeExtensions
    {
        /// <summary>
        /// 获取语言代码
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public static string GetCode(this LanguageType type)
        {
            return type switch
            {
                LanguageType.ZhHans => "zh-Hans",
                LanguageType.En => "en",
                LanguageType.Ja => "ja",
                _ => ""
            };
        }
    }
}
