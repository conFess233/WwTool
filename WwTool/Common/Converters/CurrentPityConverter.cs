using System.Globalization;
using System.Windows.Data;
using WwTool.Common.Models;

namespace WwTool.Common.Converters;

/// <summary>展示统计结果中的未完成五星周期；无记录时使用缺失值占位。</summary>
public sealed class CurrentPityConverter : IValueConverter, IMultiValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not IEnumerable<HitGoldData> history) return "—";
        var entries = history.ToList();
        if (entries.Count == 0) return "—";
        return (entries.FirstOrDefault(x => x.GachaData.ResourceId == 0)?.Pity ?? 0).ToString("N0", culture);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        Binding.DoNothing;

    /// <summary>集合数量变化时重新读取未完成周期，支持同一账号刷新。</summary>
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture) =>
        values.Length == 0 ? "—" : Convert(values[0], targetType, parameter, culture);

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) =>
        targetTypes.Select(_ => Binding.DoNothing).ToArray();
}
