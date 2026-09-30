using System;
using System.Collections.Generic;
using System.Text;
using System.Globalization;
using WwTool.Common.Enums;

namespace WwTool.Extensions
{
    /// <summary>
    /// 语言类型扩展方法
    /// </summary>
    public static class LanguageTypeExtensions
    {
        /// <summary>按应用语言选择日期和数字格式，避免受系统语言影响。</summary>
        public static CultureInfo GetDisplayCulture(this LanguageType type) => CultureInfo.GetCultureInfo(type switch
        {
            LanguageType.En => "en-US",
            LanguageType.Ja => "ja-JP",
            _ => "zh-CN"
        });

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
