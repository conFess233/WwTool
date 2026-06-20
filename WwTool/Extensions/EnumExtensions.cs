using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reflection;
using System.Text;

namespace WwTool.Extensions
{
    /// <summary>
    /// 枚举扩展方法
    /// </summary>
    public static class EnumExtensions
    {
        /// <summary>
        /// 获取枚举的描述信息
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public static string GetDescription(this Enum value)
        {
            FieldInfo field = value.GetType().GetField(value.ToString());

            DescriptionAttribute attribute = field.GetCustomAttribute<DescriptionAttribute>();

            return attribute?.Description ?? value.ToString();
        }

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

            return GetDescription(value);
        }
    }
}
