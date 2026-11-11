using System.Globalization;
using Avalonia.Controls;
using Avalonia.Data.Converters;

namespace WwTool.UI.Pages.Pulls;

public partial class PoolRecordsView : UserControl
{
    /// <summary>距底部多少像素时加载下一页。</summary>
    private const double LoadMoreThreshold = 240;

    public PoolRecordsView()
    {
        InitializeComponent();
        ListScroller.ScrollChanged += OnScrollChanged;
    }

    // 列表内部滚动：接近底部时加载更早的记录。
    private void OnScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        if (DataContext is not PoolRecordsViewModel { HasMore: true } vm) return;
        if (ListScroller.Offset.Y + ListScroller.Viewport.Height >= ListScroller.Extent.Height - LoadMoreThreshold) vm.LoadMore();
    }
}

/// <summary>取名称首字作为头像占位（图标缺失时使用）。</summary>
public sealed class InitialConverter : IValueConverter
{
    public static readonly InitialConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is string { Length: > 0 } s ? char.IsSurrogate(s[0]) && s.Length > 1 ? s[..2] : s[..1].ToUpperInvariant() : "?";

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
