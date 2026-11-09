using System.Collections.ObjectModel;
using System.Globalization;
using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace WwTool.UI.Shell;

/// <summary>
/// 顶部悬浮岛导航。只负责显示与选中状态，选中变化通过 <see cref="NavItem.IsSelected"/> 反映给外部。
/// </summary>
public partial class FloatingNavIsland : UserControl
{
    public FloatingNavIsland()
    {
        InitializeComponent();
        // 控件自身不设 DataContext（留给外部），因此直接把集合交给 ItemsControl。
        ItemsHost.ItemsSource = Items;
    }

    /// <summary>导航项，按显示顺序排列。</summary>
    public ObservableCollection<NavItem> Items { get; } = [];
}

/// <summary>
/// 把 <c>avares://</c> 地址转换为位图，并按地址缓存，避免每次模板重建都重新解码。
/// </summary>
public sealed class AssetImageConverter : IValueConverter
{
    public static readonly AssetImageConverter Instance = new();
    private readonly Dictionary<string, Bitmap> _cache = new(StringComparer.Ordinal);

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string uri || string.IsNullOrWhiteSpace(uri)) return null;
        if (_cache.TryGetValue(uri, out var cached)) return cached;
        using Stream stream = AssetLoader.Open(new Uri(uri));
        var bitmap = new Bitmap(stream);
        _cache[uri] = bitmap;
        return bitmap;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
