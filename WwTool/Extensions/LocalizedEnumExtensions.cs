using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reflection;
using System.Text;
using WwTool.Core.Extensions;

namespace WwTool.Extensions
{
    /// <summary>
    /// 旧 WPF 界面使用的多语言枚举描述；无语言依赖的 GetDescription 已移到 WwTool.Core.Extensions。
    /// </summary>
    public static class LocalizedEnumExtensions
    {
        /// <summary>
        /// 获取枚举的多语言描述信息
        /// </summary>
        public static string GetLocalizedDescription(this Enum value)
        {
            if (value == null) return string.Empty;
            string enumName = value.GetType().Name;
            string valueName = value.ToString();

            if (enumName == "CardPoolType")
            {
                valueName = valueName switch
                {
                    "CharacterEvent" => "RoleEvent",
                    "CharacterStandard" => "RolePermanent",
                    "WeaponStandard" => "WeaponPermanent",
                    "Beginner" => "Novice",
                    "BeginnerChoice" => "NoviceSelect",
                    "CharacterNoviceJourney" => "RoleNewbie",
                    "WeaponNoviceJourney" => "WeaponNewbie",
                    _ => valueName
                };
            }

            string key = $"Enum_{enumName}_{valueName}";
            string localized = WwTool.Common.Utils.LanguageManager.Instance[key];
            if (localized != key && !string.IsNullOrEmpty(localized))
            {
                return localized;
            }

            return value.GetDescription();
        }
    }
}
