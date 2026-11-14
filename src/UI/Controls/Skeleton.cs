using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using WwTool.UI.Theming;

namespace WwTool.UI.Controls;

/// <summary>
/// 加载骨架占位块（动效「加载骨架」）：暖色底上一道微光斜扫，1.4–1.6 秒一次；减少动画时静止。
/// </summary>
public sealed class Skeleton : Control
{
    public static readonly StyledProperty<double> RadiusProperty =
        AvaloniaProperty.Register<Skeleton, double>(nameof(Radius), 6);

    public static readonly StyledProperty<Color> BaseColorProperty =
        AvaloniaProperty.Register<Skeleton, Color>(nameof(BaseColor), Color.Parse("#EFE7D8"));

    public static readonly StyledProperty<Color> ShineColorProperty =
        AvaloniaProperty.Register<Skeleton, Color>(nameof(ShineColor), Color.Parse("#F8F3E8"));

    private const double PeriodSeconds = 1.4;
    private bool _frameRequested;
    private TimeSpan _clock;

    static Skeleton()
    {
        AffectsRender<Skeleton>(RadiusProperty, BaseColorProperty, ShineColorProperty);
        IsHitTestVisibleProperty.OverrideDefaultValue<Skeleton>(false);
    }

    public double Radius { get => GetValue(RadiusProperty); set => SetValue(RadiusProperty, value); }
    public Color BaseColor { get => GetValue(BaseColorProperty); set => SetValue(BaseColorProperty, value); }
    public Color ShineColor { get => GetValue(ShineColorProperty); set => SetValue(ShineColorProperty, value); }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Motion.LoopsChanged += OnLoopsChanged;
        RequestFrame();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        Motion.LoopsChanged -= OnLoopsChanged;
    }

    /// <summary>窗口在后台时光带停在原处，回到前台继续。</summary>
    private void OnLoopsChanged(object? sender, EventArgs e) => RequestFrame();

    private void RequestFrame()
    {
        if (_frameRequested || !Motion.LoopsAllowed || TopLevel.GetTopLevel(this) is not { } top) return;
        _frameRequested = true;
        top.RequestAnimationFrame(time =>
        {
            _frameRequested = false;
            _clock = time;
            InvalidateVisual();
            if (IsEffectivelyVisible) RequestFrame();
        });
    }

    public override void Render(DrawingContext context)
    {
        var rect = new RoundedRect(new Rect(Bounds.Size), Radius);
        context.DrawRectangle(new SolidColorBrush(BaseColor), null, rect);
        if (Motion.Reduced) return;
        // 背景宽 200%、自右向左移动一个周期，与 CSS 的 background-position 动画一致。
        double phase = _clock.TotalSeconds % PeriodSeconds / PeriodSeconds;
        double w = Math.Max(1, Bounds.Width);
        double offset = w - 2 * w * phase;
        var shine = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(offset - w, 0, RelativeUnit.Absolute),
            EndPoint = new RelativePoint(offset + w, 0, RelativeUnit.Absolute),
            GradientStops =
            {
                new GradientStop(BaseColor, 0),
                new GradientStop(ShineColor, 0.5),
                new GradientStop(BaseColor, 1),
            },
        };
        using (context.PushClip(rect))
        {
            context.DrawRectangle(shine, null, new Rect(Bounds.Size));
        }
    }
}
