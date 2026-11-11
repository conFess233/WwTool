using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace WwTool.UI.Controls;

/// <summary>
/// 比例条：圆角轨道上按 <see cref="Ratio"/>（0–1）绘制填充。数值须另以文字显示。
/// </summary>
public sealed class RatioBar : Control
{
    public static readonly StyledProperty<double> RatioProperty =
        AvaloniaProperty.Register<RatioBar, double>(nameof(Ratio));

    public static readonly StyledProperty<IBrush?> FillProperty =
        AvaloniaProperty.Register<RatioBar, IBrush?>(nameof(Fill));

    public static readonly StyledProperty<IBrush?> TrackProperty =
        AvaloniaProperty.Register<RatioBar, IBrush?>(nameof(Track));

    static RatioBar()
    {
        AffectsRender<RatioBar>(RatioProperty, FillProperty, TrackProperty);
        HeightProperty.OverrideDefaultValue<RatioBar>(10);
    }

    public double Ratio { get => GetValue(RatioProperty); set => SetValue(RatioProperty, value); }

    public IBrush? Fill { get => GetValue(FillProperty); set => SetValue(FillProperty, value); }

    public IBrush? Track { get => GetValue(TrackProperty); set => SetValue(TrackProperty, value); }

    protected override Size MeasureOverride(Size availableSize) =>
        new(double.IsInfinity(availableSize.Width) ? 120 : availableSize.Width, Height);

    public override void Render(DrawingContext context)
    {
        double width = Bounds.Width, height = Bounds.Height;
        if (width <= 0 || height <= 0) return;
        double radius = height / 2;
        if (Track is not null) context.DrawRectangle(Track, null, new Rect(0, 0, width, height), radius, radius);
        double filled = width * Math.Clamp(double.IsNaN(Ratio) ? 0 : Ratio, 0, 1);
        if (Fill is not null && filled > 0)
        {
            context.DrawRectangle(Fill, null, new Rect(0, 0, Math.Max(filled, height), height), radius, radius);
        }
    }
}
