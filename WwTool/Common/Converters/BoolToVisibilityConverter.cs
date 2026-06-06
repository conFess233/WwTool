using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace WwTool.Common.Converters
{
    public class BoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool flag = false;
            if (value is bool b)
            {
                flag = b;
            }
            else if (value != null && bool.TryParse(value.ToString(), out bool result))
            {
                flag = result;
            }

            if (parameter != null && parameter.ToString()?.ToLower() == "inverse")
            {
                flag = !flag;
            }

            return flag ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Visibility vis)
            {
                bool flag = vis == Visibility.Visible;
                if (parameter != null && parameter.ToString()?.ToLower() == "inverse")
                {
                    flag = !flag;
                }
                return flag;
            }
            return false;
        }
    }
}
