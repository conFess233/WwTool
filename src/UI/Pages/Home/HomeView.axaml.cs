using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace WwTool.UI.Pages.Home;

public partial class HomeView : UserControl
{
    public HomeView()
    {
        InitializeComponent();
        // 首页按定稿铺满可视区域（不滚动）；窗口过矮时按最小高度排布并允许滚动。
        Scroller.PropertyChanged += (_, e) =>
        {
            if (e.Property == BoundsProperty) FitRoot();
        };
    }

    private void FitRoot()
    {
        double available = Scroller.Bounds.Height - Root.Margin.Top - Root.Margin.Bottom;
        if (available > 0) Root.Height = Math.Max(Root.MinHeight, available);
    }
}

/// <summary>把 "Icon.X" 资源键转成线性图标几何。</summary>
public sealed class IconKeyConverter : IValueConverter
{
    public static IconKeyConverter Instance { get; } = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is string key && Application.Current?.TryGetResource(key, null, out object? resource) == true ? resource as Geometry : null;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}
