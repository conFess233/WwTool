using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using WwTool.UI.Theming;

namespace WwTool.UI.Controls;

/// <summary>
/// 刻度条（开发文档「通用组件 → 刻度条」）：轨道与填充都是等宽直角刻度，从左端排起；
/// 填充按宽度比例截断，最后一格可以只亮一部分。整条控件在 <see cref="Render"/> 里自绘。
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item>标准版高 10、刻 5、隙 3；细版高 8；密版刻 5 隙 1；未出金进度刻 4 隙 1、高 14。</item>
/// <item><see cref="IsHeat"/>：填充用热力渐变，渐变铺满整条轨道，抽数越多末端越红。</item>
/// <item><see cref="Shimmer"/>：填充上叠加一道 60 宽暖白光带，从左到右循环扫过，越接近满扫得越快（3.2 秒到 1 秒）。</item>
/// <item><see cref="AnimateIn"/>：首次显示时从 0 逐格点亮到目标值（900ms，先快后慢）；之后数值变化平滑过渡。</item>
/// </list>
/// 减少动画时不扫光带、不做点亮动画。数值须另以文字显示，不只靠颜色。
/// </remarks>
public sealed class SegmentedBar : Control
{
    public static readonly StyledProperty<double> ValueProperty =
        AvaloniaProperty.Register<SegmentedBar, double>(nameof(Value));

    public static readonly StyledProperty<double> MaximumProperty =
        AvaloniaProperty.Register<SegmentedBar, double>(nameof(Maximum), 100);

    public static readonly StyledProperty<double> SegmentWidthProperty =
        AvaloniaProperty.Register<SegmentedBar, double>(nameof(SegmentWidth), 5);

    public static readonly StyledProperty<double> SegmentGapProperty =
        AvaloniaProperty.Register<SegmentedBar, double>(nameof(SegmentGap), 3);

    public static readonly StyledProperty<IBrush?> FillProperty =
        AvaloniaProperty.Register<SegmentedBar, IBrush?>(nameof(Fill));

    public static readonly StyledProperty<IBrush?> TrackProperty =
        AvaloniaProperty.Register<SegmentedBar, IBrush?>(nameof(Track));

    public static readonly StyledProperty<bool> IsHeatProperty =
        AvaloniaProperty.Register<SegmentedBar, bool>(nameof(IsHeat));

    public static readonly StyledProperty<Color> HeatStartProperty =
        AvaloniaProperty.Register<SegmentedBar, Color>(nameof(HeatStart), Color.Parse("#9A6A14"));

    public static readonly StyledProperty<bool> ShimmerProperty =
        AvaloniaProperty.Register<SegmentedBar, bool>(nameof(Shimmer));

    public static readonly StyledProperty<bool> AnimateInProperty =
        AvaloniaProperty.Register<SegmentedBar, bool>(nameof(AnimateIn), true);

    /// <summary>热力渐变的固定段（起点取强调色）：45% #B8801F、72% #D0752A、88% #C2452A、100% #A8302A。</summary>
    private static readonly (double Offset, Color Color)[] HeatStops =
    [
        (0.45, Color.Parse("#B8801F")), (0.72, Color.Parse("#D0752A")), (0.88, Color.Parse("#C2452A")), (1.0, Color.Parse("#A8302A")),
    ];

    /// <summary>光带：暖白，中间约 60% 不透明，两端透明。</summary>
    private static readonly Color ShimmerCore = Color.FromArgb(150, 255, 249, 234);
    private static readonly Color ShimmerEdge = Color.FromArgb(0, 255, 249, 234);

    private double _shown;            // 当前绘制的比例（动画中间值）
    private double _from, _to;        // 比例动画起止
    private TimeSpan? _animStart;
    private TimeSpan _animDuration;
    private bool _hasShown;
    private bool _frameRequested;
    private TimeSpan _clock;

    static SegmentedBar()
    {
        AffectsRender<SegmentedBar>(SegmentWidthProperty, SegmentGapProperty, FillProperty, TrackProperty, IsHeatProperty, HeatStartProperty);
        HeightProperty.OverrideDefaultValue<SegmentedBar>(10);
    }

    public double Value { get => GetValue(ValueProperty); set => SetValue(ValueProperty, value); }

    public double Maximum { get => GetValue(MaximumProperty); set => SetValue(MaximumProperty, value); }

    public double SegmentWidth { get => GetValue(SegmentWidthProperty); set => SetValue(SegmentWidthProperty, value); }

    public double SegmentGap { get => GetValue(SegmentGapProperty); set => SetValue(SegmentGapProperty, value); }

    /// <summary>已填充刻度的画刷（常规为 Accent，保底满 / 警示为 Danger）。热力条忽略此项。</summary>
    public IBrush? Fill { get => GetValue(FillProperty); set => SetValue(FillProperty, value); }

    /// <summary>轨道（未填充刻度）画刷，对应令牌 Track。</summary>
    public IBrush? Track { get => GetValue(TrackProperty); set => SetValue(TrackProperty, value); }

    /// <summary>热力条：所有抽数 / 保底进度用。</summary>
    public bool IsHeat { get => GetValue(IsHeatProperty); set => SetValue(IsHeatProperty, value); }

    /// <summary>热力渐变起点（跟随强调色）。</summary>
    public Color HeatStart { get => GetValue(HeatStartProperty); set => SetValue(HeatStartProperty, value); }

    /// <summary>未出金进度的流动光带。</summary>
    public bool Shimmer { get => GetValue(ShimmerProperty); set => SetValue(ShimmerProperty, value); }

    /// <summary>首次显示时逐格点亮。</summary>
    public bool AnimateIn { get => GetValue(AnimateInProperty); set => SetValue(AnimateInProperty, value); }

    private double TargetRatio => Maximum > 0 ? Math.Clamp(Value / Maximum, 0, 1) : 0;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ValueProperty || change.Property == MaximumProperty)
        {
            StartTransition(_hasShown ? Motion.Relaxed : TimeSpan.FromMilliseconds(900));
        }
        else if (change.Property == ShimmerProperty)
        {
            RequestFrame();
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Motion.ReducedChanged += OnReducedChanged;
        if (!_hasShown)
        {
            _shown = AnimateIn && !Motion.Reduced ? 0 : TargetRatio;
            StartTransition(TimeSpan.FromMilliseconds(900));
            _hasShown = true;
        }
        RequestFrame();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        Motion.ReducedChanged -= OnReducedChanged;
    }

    private void OnReducedChanged(object? sender, EventArgs e) => RequestFrame();

    private void StartTransition(TimeSpan duration)
    {
        double target = TargetRatio;
        if (Motion.Reduced || !_hasShown && !AnimateIn || Math.Abs(target - _shown) < 0.0001)
        {
            _shown = target;
            _animStart = null;
            InvalidateVisual();
            return;
        }
        _from = _shown;
        _to = target;
        _animDuration = duration;
        _animStart = null; // 下一帧记录起点
        _animPending = true;
        RequestFrame();
    }

    private bool _animPending;

    private void RequestFrame()
    {
        if (_frameRequested || TopLevel.GetTopLevel(this) is not { } top) return;
        _frameRequested = true;
        top.RequestAnimationFrame(OnFrame);
    }

    private void OnFrame(TimeSpan time)
    {
        _frameRequested = false;
        _clock = time;
        if (_animPending)
        {
            _animStart = time;
            _animPending = false;
        }
        bool more = false;
        if (_animStart is { } start)
        {
            double t = _animDuration.TotalMilliseconds <= 0 ? 1 : Math.Clamp((time - start).TotalMilliseconds / _animDuration.TotalMilliseconds, 0, 1);
            _shown = _from + (_to - _from) * Motion.CountEasing.Ease(t);
            if (t >= 1) _animStart = null; else more = true;
        }
        if (Shimmer && !Motion.Reduced && _shown > 0 && IsEffectivelyVisible) more = true;
        InvalidateVisual();
        if (more && TopLevel.GetTopLevel(this) is not null) RequestFrame();
    }

    protected override Size MeasureOverride(Size availableSize) =>
        new(double.IsInfinity(availableSize.Width) ? 160 : availableSize.Width, Height);

    public override void Render(DrawingContext context)
    {
        double width = Bounds.Width, height = Bounds.Height;
        double pitch = SegmentWidth + SegmentGap;
        if (width <= 0 || height <= 0 || pitch <= 0) return;

        double filledWidth = width * Math.Clamp(_shown, 0, 1);
        IBrush? fill = IsHeat ? HeatBrush(width) : Fill;

        for (double x = 0; x < width; x += pitch)
        {
            double w = Math.Min(SegmentWidth, width - x);
            if (Track is not null) context.DrawRectangle(Track, null, new Rect(x, 0, w, height));
            if (fill is not null && x < filledWidth)
            {
                context.DrawRectangle(fill, null, new Rect(x, 0, Math.Min(w, filledWidth - x), height));
            }
        }

        if (Shimmer && !Motion.Reduced && filledWidth > 0)
        {
            // 越接近满扫得越快：比例 0 时 3.2 秒一圈，满格 1 秒。
            double period = Math.Clamp(3.2 - 2.2 * _shown, 1, 3.2);
            double phase = _clock.TotalSeconds % period / period;
            const double band = 60;
            double x0 = -band + (filledWidth + band) * phase;
            var brush = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(x0, 0, RelativeUnit.Absolute),
                EndPoint = new RelativePoint(x0 + band, 0, RelativeUnit.Absolute),
                GradientStops =
                {
                    new GradientStop(ShimmerEdge, 0),
                    new GradientStop(ShimmerCore, 0.5),
                    new GradientStop(ShimmerEdge, 1),
                },
            };
            for (double x = 0; x < filledWidth; x += pitch)
            {
                double l = Math.Max(x, x0), r = Math.Min(Math.Min(x + SegmentWidth, filledWidth), x0 + band);
                if (r > l) context.DrawRectangle(brush, null, new Rect(l, 0, r - l, height));
            }
        }
    }

    private IBrush HeatBrush(double width)
    {
        var brush = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Absolute),
            EndPoint = new RelativePoint(width, 0, RelativeUnit.Absolute),
        };
        brush.GradientStops.Add(new GradientStop(HeatStart, 0));
        foreach (var (offset, color) in HeatStops) brush.GradientStops.Add(new GradientStop(color, offset));
        return brush;
    }
}
