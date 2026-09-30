using System;
using System.Globalization;
using System.Windows.Data;
using WwTool.Services;
using WwTool.Common.Utils;
using WwTool.Extensions;

namespace WwTool.Common.Converters
{
    public class ItemIdToNameConverter : IValueConverter, IMultiValueConverter
    {
        public GameDataService? Catalog { get; set; }
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int id)
            {
                var item = Catalog?.GetItemById(id);
                string? name = item?.GetName(LanguageManager.Instance.CurrentLanguage.GetCode());
                if (!string.IsNullOrWhiteSpace(name)) return name;
                return id.ToString();
            }

            return value?.ToString() ?? string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }

        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values != null && values.Length > 0 && values[0] is int id)
            {
                var name = Convert(id, targetType, parameter, culture);
                // 如果无法解析名称且返回了 id 字符串，并且提供了回退名称
                if (name.ToString() == id.ToString() && values.Length > 2 && values[2] is string fallbackName)
                {
                    // 特定处理 Msg_Pity (“已垫”等) 的动态多语言查询
                    if (id == 0) return LanguageManager.Instance["Msg_Pity"];
                    return fallbackName;
                }
                return name;
            }
            return string.Empty;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
