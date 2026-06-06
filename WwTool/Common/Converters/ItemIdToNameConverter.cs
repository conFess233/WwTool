using System;
using System.Globalization;
using System.Windows.Data;
using Prism.Ioc;
using WwTool.Services;
using WwTool.Common.Utils;
using WwTool.Extensions;

namespace WwTool.Common.Converters
{
    public class ItemIdToNameConverter : IValueConverter, IMultiValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int id)
            {
                try
                {
                    var gameDataService = ContainerLocator.Current.Resolve<GameDataService>();
                    if (gameDataService != null)
                    {
                        var itemInfo = gameDataService.GetItemById(id);
                        if (itemInfo != null)
                        {
                            string code = LanguageManager.Instance.CurrentLanguage.GetCode();
                            return itemInfo.GetName(code);
                        }
                    }
                }
                catch
                {
                    // 忽略解析错误
                }
                
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
