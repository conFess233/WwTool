using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using WwTool.UI.Theming;

namespace WwTool.UI.Controls;

/// <summary>
/// 拖拽导入的虚线框（动效「拖拽导入」）：圆角矩形底色 + 2 宽 8 / 8 虚线描边，<see cref="IsFlowing"/> 时虚线沿边缘流动（0.8 秒一周期）。
/// 减少动画时虚线不流动。
/// </summary>
public sealed class MarchingAnts : Control
{
    public static readonly StyledProperty<IBrush?> StrokeProperty =
        AvaloniaProperty.Register<MarchingAnts, IBrush?>(nameof(Stroke));

    public static readonly StyledProperty<IBrush?> FillProperty =
        AvaloniaProperty.Register<MarchingAnts, IBrush?>(nameof(Fill));

    public static readonly StyledProperty<double> RadiusProperty =
        AvaloniaProperty.Register<MarchingAnts, double>(nameof(Radius), 12);

    public static readonly StyledProperty<bool> IsFlowingProperty =
        AvaloniaProperty.Register<MarchingAnts, bool>(nameof(IsFlowing), true);

    public IBrush? Stroke { get => GetValue(StrokeProperty); set => SetValue(StrokeProperty, value); }
    public IBrush? Fill { get => GetValue(FillProperty); set => SetValue(FillProperty, value); }
    public double Radius { get => GetValue(RadiusProperty); set => SetValue(RadiusProperty, value); }
    public bool IsFlowing { get => GetValue(IsFlowingProperty); set => SetValue(IsFlowingProperty, value); }

    private const double Dash = 8;
    private const double PeriodMs = 800;
    private double _offset;
    private bool _ticking;
    private TimeSpan? _last;

    static MarchingAnts() => AffectsRender<MarchingAnts>(StrokeProperty, FillProperty, RadiusProperty);

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        StartTicking();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsFlowingProperty || change.Property == IsVisibleProperty) StartTicking();
    }

    private void StartTicking()
    {
        if (_ticking || !IsFlowing || !IsEffectivelyVisible) return;
        _ticking = true;
        _last = null;
        TopLevel.GetTopLevel(this)?.RequestAnimationFrame(Tick);
    }

    private void Tick(TimeSpan now)
    {
        if (!IsFlowing || !IsEffectivelyVisible || TopLevel.GetTopLevel(this) is not { } top)
        {
            _ticking = false;
            return;
        }
        if (!Motion.Reduced && _last is { } last)
        {
            // 一个周期走过一组虚实（16）。
            _offset = (_offset - (now - last).TotalMilliseconds / PeriodMs * Dash * 2) % (Dash * 2);
            InvalidateVisual();
        }
        _last = now;
        top.RequestAnimationFrame(Tick);
    }

    public override void Render(DrawingContext context)
    {
        var rect = new Rect(Bounds.Size).Deflate(1);
        var rounded = new RoundedRect(rect, Radius);
        if (Fill is { } fill) context.DrawRectangle(fill, null, rounded);
        if (Stroke is { } stroke)
        {
            var pen = new Pen(stroke, 2, new DashStyle([Dash / 2, Dash / 2], _offset / 2), PenLineCap.Flat);
            context.DrawRectangle(null, pen, rounded);
        }
    }
}
