using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace WwTool.UI.Controls;

/// <summary>
/// 刻度条（规范 02 §7.10）：按可用宽度排布等宽刻度，前 <see cref="Value"/>/<see cref="Maximum"/> 比例的刻度填充。
/// 整个控件在 <see cref="Render"/> 中自绘，不为每格创建元素。数值须另以文字显示，不只靠颜色。
/// </summary>
public sealed class SegmentedBar : Control
{
    public static readonly StyledProperty<double> ValueProperty =
        AvaloniaProperty.Register<SegmentedBar, double>(nameof(Value));

    public static readonly StyledProperty<double> MaximumProperty =
        AvaloniaProperty.Register<SegmentedBar, double>(nameof(Maximum), 80);

    public static readonly StyledProperty<double> SegmentWidthProperty =
        AvaloniaProperty.Register<SegmentedBar, double>(nameof(SegmentWidth), 5);

    public static readonly StyledProperty<double> SegmentGapProperty =
        AvaloniaProperty.Register<SegmentedBar, double>(nameof(SegmentGap), 3);

    public static readonly StyledProperty<IBrush?> FillProperty =
        AvaloniaProperty.Register<SegmentedBar, IBrush?>(nameof(Fill));

    public static readonly StyledProperty<IBrush?> TrackProperty =
        AvaloniaProperty.Register<SegmentedBar, IBrush?>(nameof(Track));

    static SegmentedBar()
    {
        AffectsRender<SegmentedBar>(ValueProperty, MaximumProperty, SegmentWidthProperty, SegmentGapProperty, FillProperty, TrackProperty);
        HeightProperty.OverrideDefaultValue<SegmentedBar>(10);
    }

    public double Value { get => GetValue(ValueProperty); set => SetValue(ValueProperty, value); }

    public double Maximum { get => GetValue(MaximumProperty); set => SetValue(MaximumProperty, value); }

    public double SegmentWidth { get => GetValue(SegmentWidthProperty); set => SetValue(SegmentWidthProperty, value); }

    public double SegmentGap { get => GetValue(SegmentGapProperty); set => SetValue(SegmentGapProperty, value); }

    /// <summary>已填充刻度的画刷（常规为 Accent，保底满 / 警示用 Danger）。</summary>
    public IBrush? Fill { get => GetValue(FillProperty); set => SetValue(FillProperty, value); }

    /// <summary>轨道（未填充刻度）画刷，对应令牌 SegmentTrack。</summary>
    public IBrush? Track { get => GetValue(TrackProperty); set => SetValue(TrackProperty, value); }

    protected override Size MeasureOverride(Size availableSize) =>
        new(double.IsInfinity(availableSize.Width) ? 160 : availableSize.Width, Height);

    public override void Render(DrawingContext context)
    {
        double width = Bounds.Width, height = Bounds.Height;
        double pitch = SegmentWidth + SegmentGap;
        if (width <= 0 || height <= 0 || pitch <= 0) return;

        int count = Math.Max(1, (int)((width + SegmentGap) / pitch));
        double ratio = Maximum > 0 ? Math.Clamp(Value / Maximum, 0, 1) : 0;
        int filled = (int)Math.Round(ratio * count, MidpointRounding.AwayFromZero);
        if (Value > 0 && filled == 0) filled = 1; // 有值时至少点亮一格
        // 刻度整体居中，避免右侧留出不足一格的空隙。
        double offset = (width - (count * pitch - SegmentGap)) / 2;
        double radius = Math.Min(SegmentWidth, height) / 2.5;

        for (int i = 0; i < count; i++)
        {
            IBrush? brush = i < filled ? Fill : Track;
            if (brush is null) continue;
            var rect = new Rect(offset + i * pitch, 0, SegmentWidth, height);
            context.DrawRectangle(brush, null, rect, radius, radius);
        }
    }
}
