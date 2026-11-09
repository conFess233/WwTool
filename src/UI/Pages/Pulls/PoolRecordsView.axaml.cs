using System.Globalization;
using Avalonia.Controls;
using Avalonia.Data.Converters;

namespace WwTool.UI.Pages.Pulls;

public partial class PoolRecordsView : UserControl
{
    public PoolRecordsView() => InitializeComponent();
}

/// <summary>取名称首字作为头像占位（立绘接入前使用）。</summary>
public sealed class InitialConverter : IValueConverter
{
    public static readonly InitialConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is string { Length: > 0 } s ? char.IsSurrogate(s[0]) && s.Length > 1 ? s[..2] : s[..1].ToUpperInvariant() : "?";

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
