using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using System.Text;
using System.Windows.Data;
using WwTool.Common.Utils;

namespace WwTool.Common.Converters
{
    public class EnumDescriptionConverter : IValueConverter, IMultiValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null)
                return "";

            string enumName = value.GetType().Name;
            string valueName = value.ToString() ?? "";

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
            string localized = LanguageManager.Instance[key];
            if (localized != key && !string.IsNullOrEmpty(localized))
            {
                return localized;
            }

            FieldInfo? field = value.GetType().GetField(valueName);

            DescriptionAttribute? attribute =
                field?.GetCustomAttribute<DescriptionAttribute>();

            return attribute?.Description ?? valueName;
        }

        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values == null || values.Length == 0 || values[0] == null)
                return "";

            return Convert(values[0], targetType, parameter, culture);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
