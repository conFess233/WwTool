using System.Globalization;
using System.Windows.Data;

namespace WwTool.Common.Converters;

public sealed class ProgressLabelPositionConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length < 5 ||
            !TryDouble(values[0], out double width) ||
            !TryDouble(values[1], out double value) ||
            !TryDouble(values[2], out double minimum) ||
            !TryDouble(values[3], out double maximum) ||
            !TryDouble(values[4], out double labelWidth) ||
            width <= 0 || maximum <= minimum)
        {
            return 0d;
        }

        double ratio = Math.Clamp((value - minimum) / (maximum - minimum), 0d, 1d);
        double desired = width * ratio - labelWidth * 0.5d;
        return Math.Clamp(desired, 0d, Math.Max(0d, width - labelWidth));
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();

    private static bool TryDouble(object value, out double result)
    {
        try { result = System.Convert.ToDouble(value, CultureInfo.InvariantCulture); return true; }
        catch { result = 0; return false; }
    }
}
