using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Media.Imaging;
using Avalonia.Media.Transformation;
using Avalonia.Platform;
using Avalonia.Threading;
using Avalonia.VisualTree;
using WwTool.UI.Theming;

namespace WwTool.UI.Shell;

/// <summary>
/// 顶部悬浮岛导航。选中项的金色底是一个独立胶囊，选中变化或布局变化后移动到选中项位置；
/// 除首次定位外带回弹过渡（动效「悬浮岛切换」：320ms，回弹曲线）。
/// </summary>
public partial class FloatingNavIsland : UserControl
{
    public static readonly StyledProperty<IEnumerable<NavItem>?> ItemsSourceProperty =
        AvaloniaProperty.Register<FloatingNavIsland, IEnumerable<NavItem>?>(nameof(ItemsSource));

    private static readonly TimeSpan SlideDuration = TimeSpan.FromMilliseconds(320);
    private bool _placed;

    public FloatingNavIsland()
    {
        InitializeComponent();
        ItemsHost.LayoutUpdated += (_, _) => PlacePill();
        Motion.ReducedChanged += (_, _) => ApplyTransitions();
    }

    /// <summary>导航项，按显示顺序排列。</summary>
    public IEnumerable<NavItem>? ItemsSource
    {
        get => GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property != ItemsSourceProperty) return;
        // 控件不改自身 DataContext（留给外部），因此在这里把集合交给内部 ItemsControl。
        ItemsHost.ItemsSource = ItemsSource;
        foreach (NavItem item in ItemsSource ?? []) item.PropertyChanged += OnItemChanged;
        if (ItemsSource is INotifyCollectionChanged incc)
        {
            incc.CollectionChanged += (_, e) =>
            {
                foreach (NavItem item in e.NewItems?.OfType<NavItem>() ?? []) item.PropertyChanged += OnItemChanged;
            };
        }
    }

    private void OnItemChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(NavItem.IsSelected)) Dispatcher.UIThread.Post(PlacePill, DispatcherPriority.Render);
    }

    private void ApplyTransitions()
    {
        if (!_placed || Motion.Reduced)
        {
            Pill.Transitions = null;
            return;
        }
        if (Pill.Transitions is { Count: > 0 }) return;
        Pill.Transitions =
        [
            new DoubleTransition { Property = WidthProperty, Duration = SlideDuration, Easing = Motion.BounceEasing },
            new TransformOperationsTransition { Property = RenderTransformProperty, Duration = SlideDuration, Easing = Motion.BounceEasing },
        ];
    }

    /// <summary>把选中胶囊对齐到当前选中的导航按钮；没有选中项（账号数据页）时收起。</summary>
    private void PlacePill()
    {
        RadioButton? selected = ItemsHost.GetVisualDescendants().OfType<RadioButton>().FirstOrDefault(r => r.IsChecked == true);
        if (selected is null)
        {
            Pill.Opacity = 0;
            return;
        }
        Point? origin = selected.TranslatePoint(new Point(0, 0), Pill.Parent as Visual ?? this);
        if (origin is null || selected.Bounds.Width <= 0) return;

        ApplyTransitions();
        double width = selected.Bounds.Width;
        string transform = $"translateX({origin.Value.X.ToString("0.##", CultureInfo.InvariantCulture)}px)";
        if (Math.Abs(Pill.Width - width) > 0.5) Pill.Width = width;
        if (Pill.RenderTransform is not { } current || current.Value.M31 != origin.Value.X)
        {
            Pill.RenderTransform = TransformOperations.Parse(transform);
        }
        Pill.Opacity = 1;
        if (!_placed)
        {
            _placed = true;
            Dispatcher.UIThread.Post(ApplyTransitions, DispatcherPriority.Background);
        }
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
