using System.Windows;
using System.Windows.Controls.Primitives;

namespace WwTool.UI.Components;

/// <summary>根据可用宽度重排等宽卡片，保证多语言标签的阅读空间。</summary>
public class ResponsiveUniformGrid : UniformGrid
{
    public static readonly DependencyProperty MinimumItemWidthProperty = DependencyProperty.Register(
        nameof(MinimumItemWidth), typeof(double), typeof(ResponsiveUniformGrid),
        new FrameworkPropertyMetadata(200d, FrameworkPropertyMetadataOptions.AffectsMeasure),
        value => value is double width && double.IsFinite(width) && width > 0);

    public static readonly DependencyProperty MaximumColumnsProperty = DependencyProperty.Register(
        nameof(MaximumColumns), typeof(int), typeof(ResponsiveUniformGrid),
        new FrameworkPropertyMetadata(4, FrameworkPropertyMetadataOptions.AffectsMeasure),
        value => value is int columns && columns > 0);

    public double MinimumItemWidth
    {
        get => (double)GetValue(MinimumItemWidthProperty);
        set => SetValue(MinimumItemWidthProperty, value);
    }

    public int MaximumColumns
    {
        get => (int)GetValue(MaximumColumnsProperty);
        set => SetValue(MaximumColumnsProperty, value);
    }

    protected override Size MeasureOverride(Size constraint)
    {
        int columns = double.IsFinite(constraint.Width)
            ? Math.Clamp((int)(constraint.Width / MinimumItemWidth), 1, MaximumColumns)
            : MaximumColumns;
        int visibleItems = InternalChildren.Cast<UIElement>().Count(child => child.Visibility != Visibility.Collapsed);
        // 四项指标在窄窗口排成 2×2，避免三项一行、最后一项孤立。
        if (columns > 2 && visibleItems > columns && visibleItems % columns == 1) columns--;
        Columns = columns;
        return base.MeasureOverride(constraint);
    }
}
