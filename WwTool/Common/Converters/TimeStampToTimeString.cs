using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Windows.Data;
using WwTool.Common.Utils;

namespace WwTool.Common.Converters
{
    public class TimeStampToTimeString : IValueConverter, IMultiValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is long timeStamp)
            {
                if (timeStamp <= 0) return LanguageManager.Instance["Conv_PauseRecover"];

                // 判断是秒级还是毫秒级时间戳
                DateTimeOffset targetTime = timeStamp > 20000000000
                    ? DateTimeOffset.FromUnixTimeMilliseconds(timeStamp)
                    : DateTimeOffset.FromUnixTimeSeconds(timeStamp);

                var timeSpan = targetTime - DateTimeOffset.Now;

                if (timeSpan.TotalSeconds <= 0)
                {
                    return LanguageManager.Instance["Conv_PauseRecover"];
                }

                int hours = (int)timeSpan.TotalHours;
                return string.Format(LanguageManager.Instance["Conv_RecoverAfter"], hours.ToString("D2"), timeSpan.Minutes.ToString("D2"), timeSpan.Seconds.ToString("D2"));
            }

            return null;
        }

        public object? Convert(object[] values, Type targetType, object? parameter, CultureInfo culture)
        {
            if (values == null || values.Length == 0 || values[0] == null)
                return null;

            return Convert(values[0], targetType, parameter, culture);
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }

        public object[] ConvertBack(object? value, Type[] targetTypes, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
