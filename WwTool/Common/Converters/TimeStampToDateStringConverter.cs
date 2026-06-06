using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Windows.Data;

namespace WwTool.Common.Converters
{
    public class TimeStampToDateStringConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is long timeStamp)
            {
                return DateTimeOffset.FromUnixTimeMilliseconds(timeStamp)
                    .LocalDateTime
                    .ToString("yyyy-MM-dd");
            }

            return null;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
