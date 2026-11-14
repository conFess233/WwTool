using Avalonia;
using Avalonia.Controls;

namespace WwTool.UI.Controls;

/// <summary>
/// 按比例摆放子元素的面板：附加属性 <c>Left</c> / <c>Right</c> / <c>Top</c> / <c>Bottom</c> 为 0–1 的位置比例
/// （<c>Top</c> / <c>Bottom</c> 不设时铺满高度）。用于活动时间线的横条、「今天」竖线和版本日历图上的可点区域。
/// </summary>
public sealed class RatioPanel : Panel
{
    public static readonly AttachedProperty<double> LeftProperty =
        AvaloniaProperty.RegisterAttached<RatioPanel, Control, double>("Left", 0);

    public static readonly AttachedProperty<double> RightProperty =
        AvaloniaProperty.RegisterAttached<RatioPanel, Control, double>("Right", 1);

    public static readonly AttachedProperty<double> TopProperty =
        AvaloniaProperty.RegisterAttached<RatioPanel, Control, double>("Top", double.NaN);

    public static readonly AttachedProperty<double> BottomProperty =
        AvaloniaProperty.RegisterAttached<RatioPanel, Control, double>("Bottom", double.NaN);

    /// <summary>子元素的最小宽度（像素），太短的横条仍能看见、能点。</summary>
    public static readonly StyledProperty<double> MinChildWidthProperty =
        AvaloniaProperty.Register<RatioPanel, double>(nameof(MinChildWidth), 0);

    /// <summary>Left 与 Right 相等（一个点）的子元素以该点为中心摆放（按自身宽度，不超出面板）。用于刻度文字和「现在」标记。</summary>
    public static readonly StyledProperty<bool> CenterPointsProperty =
        AvaloniaProperty.Register<RatioPanel, bool>(nameof(CenterPoints));

    public bool CenterPoints
    {
        get => GetValue(CenterPointsProperty);
        set => SetValue(CenterPointsProperty, value);
    }

    /// <summary>
    /// 按点居中的子元素放不下时均匀抽稀（每隔 k 个显示一个，其余移出可视区并裁掉）。用于窄窗口下的刻度文字。
    /// </summary>
    public static readonly StyledProperty<bool> ThinOverlappingProperty =
        AvaloniaProperty.Register<RatioPanel, bool>(nameof(ThinOverlapping));

    public bool ThinOverlapping
    {
        get => GetValue(ThinOverlappingProperty);
        set => SetValue(ThinOverlappingProperty, value);
    }

    static RatioPanel()
    {
        AffectsParentArrange<RatioPanel>(LeftProperty, RightProperty, TopProperty, BottomProperty);
    }

    public double MinChildWidth
    {
        get => GetValue(MinChildWidthProperty);
        set => SetValue(MinChildWidthProperty, value);
    }

    public static double GetLeft(Control c) => c.GetValue(LeftProperty);
    public static void SetLeft(Control c, double v) => c.SetValue(LeftProperty, v);
    public static double GetRight(Control c) => c.GetValue(RightProperty);
    public static void SetRight(Control c, double v) => c.SetValue(RightProperty, v);
    public static double GetTop(Control c) => c.GetValue(TopProperty);
    public static void SetTop(Control c, double v) => c.SetValue(TopProperty, v);
    public static double GetBottom(Control c) => c.GetValue(BottomProperty);
    public static void SetBottom(Control c, double v) => c.SetValue(BottomProperty, v);

    protected override Size MeasureOverride(Size availableSize)
    {
        double height = 0;
        foreach (Control child in Children)
        {
            double width = double.IsInfinity(availableSize.Width) || (CenterPoints && GetLeft(child) == GetRight(child))
                ? double.PositiveInfinity
                : Span(child, availableSize.Width).Width;
            child.Measure(new Size(width, availableSize.Height));
            if (double.IsNaN(GetTop(child))) height = Math.Max(height, child.DesiredSize.Height);
        }
        return new Size(double.IsInfinity(availableSize.Width) ? 0 : availableSize.Width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        int step = ThinOverlapping ? ThinStep(finalSize.Width) : 1;
        int index = 0;
        foreach (Control child in Children)
        {
            if (step > 1 && index++ % step != 0)
            {
                child.Arrange(new Rect(-100000, 0, child.DesiredSize.Width, child.DesiredSize.Height));
                continue;
            }
            (double x, double width) = Span(child, finalSize.Width);
            double top = GetTop(child), bottom = GetBottom(child);
            double y = 0, height = finalSize.Height;
            if (!double.IsNaN(top))
            {
                y = Math.Clamp(top, 0, 1) * finalSize.Height;
                double end = double.IsNaN(bottom) ? 1 : Math.Clamp(bottom, 0, 1);
                height = Math.Max(0, end * finalSize.Height - y);
            }
            child.Arrange(new Rect(x, y, width, height));
        }
        return finalSize;
    }

    /// <summary>相邻子元素的最小间距比最宽的子元素（加 8 像素间隙）还小时，每隔几个显示一个。</summary>
    private int ThinStep(double total)
    {
        var points = Children.Where(c => c.IsVisible).Select(c => Math.Clamp(GetLeft(c), 0, 1) * total).ToList();
        if (points.Count < 2) return 1;
        double widest = Children.Max(c => c.DesiredSize.Width) + 8;
        double gap = double.MaxValue;
        for (int i = 1; i < points.Count; i++) gap = Math.Min(gap, Math.Abs(points[i] - points[i - 1]));
        return gap <= 0 ? 1 : Math.Max(1, (int)Math.Ceiling(widest / gap));
    }

    private (double X, double Width) Span(Control child, double total)
    {
        double left = Math.Clamp(GetLeft(child), 0, 1), right = Math.Clamp(GetRight(child), 0, 1);
        double x = left * total;
        if (CenterPoints && left == right)
        {
            double w = Math.Min(child.DesiredSize.Width, total);
            return (Math.Clamp(x - w / 2, 0, Math.Max(0, total - w)), w);
        }
        double width = Math.Max(0, (right - left) * total);
        if (width < MinChildWidth)
        {
            width = Math.Min(MinChildWidth, total);
            x = Math.Min(x, total - width);
        }
        return (x, width);
    }
}
