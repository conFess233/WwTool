using Avalonia;
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
    public static readonly StyledProperty<IEnumerable<NavItem>?> ItemsSourceProperty =
        AvaloniaProperty.Register<FloatingNavIsland, IEnumerable<NavItem>?>(nameof(ItemsSource));

    public FloatingNavIsland() => InitializeComponent();

    /// <summary>导航项，按显示顺序排列。</summary>
    public IEnumerable<NavItem>? ItemsSource
    {
        get => GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        // 控件不改自身 DataContext（留给外部），因此在这里把集合交给内部 ItemsControl。
        if (change.Property == ItemsSourceProperty) ItemsHost.ItemsSource = ItemsSource;
    }
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
