using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Windows.Data;
using System.Windows.Media;

namespace WwTool.Common.Converters
{
    public class ProgressColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double pityValue)
            {
                if (pityValue <= 55)
                {
                    return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#12B76A")); // 绿
                }

                else if (pityValue <= 65)
                {
                    return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F59E0B")); // 黄
                }

                else
                {
                    return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F04438")); // 红
                }
            }

            return Brushes.Green; // 默认缺省颜色
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
