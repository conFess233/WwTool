using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;
using WwTool.UI.Theming;

namespace WwTool.UI.Controls;

/// <summary>
/// 动效「角色卡悬停倾斜」「悬停抬起」：指针移入时卡片上浮 4，并随指针三维倾斜（最大 <see cref="MaxAngle"/>°），
/// 子树中的 <see cref="GlossSweep"/> 扫过一道白色光泽（700ms）；移出时平滑回正。减少动画时不倾斜、不上浮、不扫光。
/// </summary>
public sealed class TiltHost : Decorator
{
    public static readonly StyledProperty<double> MaxAngleProperty =
        AvaloniaProperty.Register<TiltHost, double>(nameof(MaxAngle), 14);

    public static readonly StyledProperty<double> LiftProperty =
        AvaloniaProperty.Register<TiltHost, double>(nameof(Lift), 4);

    public double MaxAngle { get => GetValue(MaxAngleProperty); set => SetValue(MaxAngleProperty, value); }
    public double Lift { get => GetValue(LiftProperty); set => SetValue(LiftProperty, value); }

    private readonly Rotate3DTransform _rotate = new() { Depth = 700 };
    private readonly TranslateTransform _translate = new();
    private double _targetX, _targetY, _targetLift;
    private bool _frameRequested;
    private TimeSpan? _last;

    public TiltHost()
    {
        RenderTransformOrigin = RelativePoint.Center;
        RenderTransform = new TransformGroup { Children = { _rotate, _translate } };
        ClipToBounds = false;
    }

    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        if (Motion.Reduced) return;
        _targetLift = -Lift;
        this.FindDescendantOfType<GlossSweep>()?.Sweep();
        Track(e);
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (!Motion.Reduced) Track(e);
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        _targetX = _targetY = _targetLift = 0;
        RequestFrame();
    }

    private void Track(PointerEventArgs e)
    {
        Point p = e.GetPosition(this);
        double w = Math.Max(1, Bounds.Width), h = Math.Max(1, Bounds.Height);
        double nx = Math.Clamp(p.X / w * 2 - 1, -1, 1);
        double ny = Math.Clamp(p.Y / h * 2 - 1, -1, 1);
        // 指针在右侧时右边向里压（绕 Y 轴正转），在上方时上边向里压。
        _targetY = nx * MaxAngle;
        _targetX = -ny * MaxAngle * 0.7;
        RequestFrame();
    }

    private void RequestFrame()
    {
        if (_frameRequested || TopLevel.GetTopLevel(this) is not { } top) return;
        _frameRequested = true;
        top.RequestAnimationFrame(OnFrame);
    }

    private void OnFrame(TimeSpan time)
    {
        _frameRequested = false;
        double dt = _last is { } last ? Math.Clamp((time - last).TotalSeconds, 0, 0.05) : 0.016;
        _last = time;
        // 指数平滑：约 120ms 跟上指针。
        double k = 1 - Math.Exp(-dt / 0.06);
        _rotate.AngleX += (_targetX - _rotate.AngleX) * k;
        _rotate.AngleY += (_targetY - _rotate.AngleY) * k;
        _translate.Y += (_targetLift - _translate.Y) * k;
        bool settled = Math.Abs(_targetX - _rotate.AngleX) < 0.05 && Math.Abs(_targetY - _rotate.AngleY) < 0.05
                       && Math.Abs(_targetLift - _translate.Y) < 0.05;
        if (settled)
        {
            _rotate.AngleX = _targetX;
            _rotate.AngleY = _targetY;
            _translate.Y = _targetLift;
            _last = null;
            return;
        }
        RequestFrame();
    }
}

/// <summary>悬停时从左到右扫过的一道白色光泽（110° 斜向，宽 60%，700ms）。放在卡片内容之上，不参与命中测试。</summary>
public sealed class GlossSweep : Control
{
    private static readonly TimeSpan Duration = TimeSpan.FromMilliseconds(700);
    private bool _frameRequested;
    private bool _pending;
    private TimeSpan? _start;
    private double _progress = -1;

    static GlossSweep()
    {
        IsHitTestVisibleProperty.OverrideDefaultValue<GlossSweep>(false);
    }

    public void Sweep()
    {
        if (Motion.Reduced) return;
        _pending = true;
        RequestFrame();
    }

    private void RequestFrame()
    {
        if (_frameRequested || TopLevel.GetTopLevel(this) is not { } top) return;
        _frameRequested = true;
        top.RequestAnimationFrame(time =>
        {
            _frameRequested = false;
            if (_pending)
            {
                _start = time;
                _pending = false;
            }
            if (_start is not { } start) return;
            double t = (time - start).TotalMilliseconds / Duration.TotalMilliseconds;
            _progress = t >= 1 ? -1 : Motion.StandardEasing.Ease(Math.Clamp(t, 0, 1));
            if (t >= 1) _start = null;
            InvalidateVisual();
            if (_start is not null) RequestFrame();
        });
    }

    public override void Render(DrawingContext context)
    {
        if (_progress < 0 || Bounds.Width <= 0) return;
        double w = Bounds.Width, h = Bounds.Height;
        double band = w * 0.6;
        // CSS：left 从 -80% 移到 130%。
        double x = w * (-0.8 + 2.1 * _progress);
        var brush = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(x, 0, RelativeUnit.Absolute),
            EndPoint = new RelativePoint(x + band, h * 0.36, RelativeUnit.Absolute),
            GradientStops =
            {
                new GradientStop(Color.FromArgb(0, 255, 255, 255), 0),
                new GradientStop(Color.FromArgb(178, 255, 255, 255), 0.5),
                new GradientStop(Color.FromArgb(0, 255, 255, 255), 1),
            },
        };
        context.DrawRectangle(brush, null, new Rect(Bounds.Size));
    }
}
