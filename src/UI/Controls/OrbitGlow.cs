using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using WwTool.UI.Theming;

namespace WwTool.UI.Controls;

/// <summary>
/// 动效「波片回满」：卡片描边出现环绕的金色流光（2.6 秒一圈），外侧光晕呼吸。
/// 同一张卡片放两层：<see cref="IsHalo"/> 为 true 的一层放在卡片下面画外侧光晕，另一层放在卡片上面画流光描边。
/// <see cref="IsActive"/> 为 false 时什么都不画；减少动画时流光不转、光晕不呼吸，只保留静态金色描边。
/// 流光与呼吸只在回满时（以及窗口回到前台时）播放约 8 秒，之后淡出成静态描边：一直转会让整个窗口按帧重绘。
/// </summary>
public sealed class OrbitGlow : Control
{
    public static readonly StyledProperty<bool> IsActiveProperty =
        AvaloniaProperty.Register<OrbitGlow, bool>(nameof(IsActive));

    public static readonly StyledProperty<bool> IsHaloProperty =
        AvaloniaProperty.Register<OrbitGlow, bool>(nameof(IsHalo));

    public static readonly StyledProperty<double> RadiusProperty =
        AvaloniaProperty.Register<OrbitGlow, double>(nameof(Radius), 10);

    public static readonly StyledProperty<Color> ColorProperty =
        AvaloniaProperty.Register<OrbitGlow, Color>(nameof(Color), Color.Parse("#9A6A14"));

    public static readonly StyledProperty<Color> LightColorProperty =
        AvaloniaProperty.Register<OrbitGlow, Color>(nameof(LightColor), Color.Parse("#F5D27A"));

    private const double OrbitSeconds = 2.6;
    private const double BreathSeconds = 2.0;

    static OrbitGlow()
    {
        AffectsRender<OrbitGlow>(IsActiveProperty, RadiusProperty, ColorProperty, LightColorProperty);
        IsHitTestVisibleProperty.OverrideDefaultValue<OrbitGlow>(false);
    }

    public bool IsActive { get => GetValue(IsActiveProperty); set => SetValue(IsActiveProperty, value); }
    public bool IsHalo { get => GetValue(IsHaloProperty); set => SetValue(IsHaloProperty, value); }
    public double Radius { get => GetValue(RadiusProperty); set => SetValue(RadiusProperty, value); }
    public Color Color { get => GetValue(ColorProperty); set => SetValue(ColorProperty, value); }
    public Color LightColor { get => GetValue(LightColorProperty); set => SetValue(LightColorProperty, value); }

    /// <summary>每次播放的时长：约 3 圈流光、4 次呼吸；最后 0.6 秒流光淡出。</summary>
    private const double PlaySeconds = 8;
    private const double FadeSeconds = 0.6;

    private bool _frameRequested;
    private TimeSpan _clock;
    /// <summary>本次播放的起点（帧时间）；<see cref="_pending"/> 时下一帧记录。</summary>
    private TimeSpan? _from;
    private bool _pending;

    private double Elapsed => _from is { } from ? (_clock - from).TotalSeconds : 0;

    /// <summary>正在播放流光与呼吸。</summary>
    private bool Playing => Motion.LoopsAllowed && (_pending || Elapsed < PlaySeconds);

    private void Replay()
    {
        _from = null;
        _pending = true;
        InvalidateVisual();
        RequestFrame();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsActiveProperty && IsActive) Replay();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Motion.LoopsChanged += OnLoopsChanged;
        Replay();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        Motion.LoopsChanged -= OnLoopsChanged;
    }

    private void OnLoopsChanged(object? sender, EventArgs e)
    {
        if (Motion.LoopsAllowed) Replay();
        else InvalidateVisual();
    }

    private void RequestFrame()
    {
        if (_frameRequested || !IsActive || !Playing || TopLevel.GetTopLevel(this) is not { } top) return;
        _frameRequested = true;
        top.RequestAnimationFrame(time =>
        {
            _frameRequested = false;
            _clock = time;
            if (_pending)
            {
                _from = time;
                _pending = false;
            }
            // 播放结束的这一帧也重绘一次，画成静态描边。
            InvalidateVisual();
            if (IsEffectivelyVisible) RequestFrame();
        });
    }

    public override void Render(DrawingContext context)
    {
        if (!IsActive || Bounds.Width <= 0 || Bounds.Height <= 0) return;
        var rect = new Rect(Bounds.Size);
        bool still = !Playing;

        if (IsHalo)
        {
            // 外侧光晕：透明度在 0.25–0.6 之间呼吸。
            double breath = still ? 0.4 : 0.425 + 0.175 * Math.Sin(Elapsed / BreathSeconds * Math.PI * 2);
            var shadow = new BoxShadow { Blur = 22, Spread = 1, Color = WithAlpha(LightColor, breath) };
            context.DrawRectangle(new SolidColorBrush(WithAlpha(Color, 0.02)), null, new RoundedRect(rect, Radius), new BoxShadows(shadow));
            return;
        }

        var outline = new RoundedRect(rect.Deflate(0.75), Math.Max(0, Radius - 0.75));
        context.DrawRectangle(null, new Pen(new SolidColorBrush(WithAlpha(Color, 0.55)), 1.5), outline);
        if (still) return;

        double angle = _clock.TotalSeconds % OrbitSeconds / OrbitSeconds * 360;
        var conic = new ConicGradientBrush
        {
            Center = RelativePoint.Center,
            Angle = angle,
            GradientStops =
            {
                new GradientStop(WithAlpha(LightColor, 0), 0),
                new GradientStop(WithAlpha(LightColor, 0), 0.62),
                new GradientStop(WithAlpha(Color, 0.9), 0.8),
                new GradientStop(LightColor, 0.92),
                new GradientStop(Colors.White, 0.96),
                new GradientStop(WithAlpha(LightColor, 0), 1),
            },
        };
        double fade = Math.Clamp((PlaySeconds - Elapsed) / FadeSeconds, 0, 1);
        using (context.PushOpacity(fade))
        {
            context.DrawRectangle(null, new Pen(conic, 2.5), new RoundedRect(rect.Deflate(1.25), Math.Max(0, Radius - 1.25)));
        }
    }

    private static Color WithAlpha(Color c, double alpha) => Color.FromArgb((byte)Math.Clamp(alpha * 255, 0, 255), c.R, c.G, c.B);
}
