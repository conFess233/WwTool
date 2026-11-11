using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using WwTool.UI.Theming;

namespace WwTool.UI.Controls;

/// <summary>图表控件共用的入场计时：首次显示或数据变化时从 0 开始，减少动画时直接到 1。</summary>
public abstract class AnimatedChart : Control
{
    private bool _frameRequested;
    private TimeSpan? _start;
    private bool _pending;

    /// <summary>入场开始后经过的时间；未在播放时为 null（视为已结束）。</summary>
    protected TimeSpan? Elapsed { get; private set; }

    protected abstract TimeSpan TotalDuration { get; }

    protected void Restart()
    {
        if (Motion.Reduced || TopLevel.GetTopLevel(this) is null)
        {
            Elapsed = null;
            InvalidateVisual();
            return;
        }
        _pending = true;
        Elapsed = TimeSpan.Zero;
        RequestFrame();
        InvalidateVisual();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Restart();
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
            Elapsed = time - start;
            if (Elapsed >= TotalDuration)
            {
                Elapsed = null;
                _start = null;
            }
            InvalidateVisual();
            if (Elapsed is not null) RequestFrame();
        });
    }

    /// <summary>某一段动画的进度（0–1，未缓动）。</summary>
    protected double Progress(TimeSpan delay, TimeSpan duration)
    {
        if (Elapsed is not { } elapsed) return 1;
        return Math.Clamp((elapsed - delay).TotalMilliseconds / Math.Max(1, duration.TotalMilliseconds), 0, 1);
    }
}

/// <summary>
/// 柱状图（动效「柱状图生长」）：柱子自左向右从底部长出，带轻微回弹，每根 500ms、间隔 40ms；
/// 0 值画成 3 高的浅色占位；<see cref="HighlightPeak"/> 时单日最高用 <see cref="PeakFill"/>。
/// <see cref="ShowValues"/> 时柱顶标数量。悬停显示 <see cref="Tips"/> 中对应的说明。
/// </summary>
public sealed class BarChart : AnimatedChart
{
    public static readonly StyledProperty<IReadOnlyList<double>?> ValuesProperty =
        AvaloniaProperty.Register<BarChart, IReadOnlyList<double>?>(nameof(Values));

    public static readonly StyledProperty<IReadOnlyList<string>?> TipsProperty =
        AvaloniaProperty.Register<BarChart, IReadOnlyList<string>?>(nameof(Tips));

    public static readonly StyledProperty<IBrush?> FillProperty =
        AvaloniaProperty.Register<BarChart, IBrush?>(nameof(Fill));

    public static readonly StyledProperty<IBrush?> PeakFillProperty =
        AvaloniaProperty.Register<BarChart, IBrush?>(nameof(PeakFill));

    public static readonly StyledProperty<IBrush?> EmptyFillProperty =
        AvaloniaProperty.Register<BarChart, IBrush?>(nameof(EmptyFill));

    public static readonly StyledProperty<IBrush?> LabelBrushProperty =
        AvaloniaProperty.Register<BarChart, IBrush?>(nameof(LabelBrush));

    public static readonly StyledProperty<bool> HighlightPeakProperty =
        AvaloniaProperty.Register<BarChart, bool>(nameof(HighlightPeak));

    public static readonly StyledProperty<bool> ShowValuesProperty =
        AvaloniaProperty.Register<BarChart, bool>(nameof(ShowValues));

    public static readonly StyledProperty<double> BarGapProperty =
        AvaloniaProperty.Register<BarChart, double>(nameof(BarGap), 5);

    public static readonly StyledProperty<double> BarRadiusProperty =
        AvaloniaProperty.Register<BarChart, double>(nameof(BarRadius), 2);

    private static readonly TimeSpan BarDuration = TimeSpan.FromMilliseconds(500);
    private static readonly TimeSpan BarStagger = TimeSpan.FromMilliseconds(40);

    static BarChart()
    {
        AffectsRender<BarChart>(FillProperty, PeakFillProperty, EmptyFillProperty, LabelBrushProperty, HighlightPeakProperty,
            ShowValuesProperty, BarGapProperty, BarRadiusProperty);
    }

    public IReadOnlyList<double>? Values { get => GetValue(ValuesProperty); set => SetValue(ValuesProperty, value); }
    public IReadOnlyList<string>? Tips { get => GetValue(TipsProperty); set => SetValue(TipsProperty, value); }
    public IBrush? Fill { get => GetValue(FillProperty); set => SetValue(FillProperty, value); }
    public IBrush? PeakFill { get => GetValue(PeakFillProperty); set => SetValue(PeakFillProperty, value); }
    public IBrush? EmptyFill { get => GetValue(EmptyFillProperty); set => SetValue(EmptyFillProperty, value); }
    public IBrush? LabelBrush { get => GetValue(LabelBrushProperty); set => SetValue(LabelBrushProperty, value); }
    public bool HighlightPeak { get => GetValue(HighlightPeakProperty); set => SetValue(HighlightPeakProperty, value); }
    public bool ShowValues { get => GetValue(ShowValuesProperty); set => SetValue(ShowValuesProperty, value); }
    public double BarGap { get => GetValue(BarGapProperty); set => SetValue(BarGapProperty, value); }
    public double BarRadius { get => GetValue(BarRadiusProperty); set => SetValue(BarRadiusProperty, value); }

    protected override TimeSpan TotalDuration =>
        BarDuration + BarStagger * Math.Max(0, (Values?.Count ?? 1) - 1);

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ValuesProperty) Restart();
    }

    private Rect BarRect(int index, int count, double value, double max, double labelSpace, out double width)
    {
        double gap = BarGap;
        width = Math.Max(1, (Bounds.Width - gap * (count - 1)) / count);
        double x = index * (width + gap);
        double plot = Math.Max(0, Bounds.Height - labelSpace);
        double h = max <= 0 || value <= 0 ? 3 : Math.Max(3, value / max * plot);
        return new Rect(x, Bounds.Height - h, width, h);
    }

    public override void Render(DrawingContext context)
    {
        // 透明底：整块区域都能悬停出提示。
        context.FillRectangle(Brushes.Transparent, new Rect(Bounds.Size));
        IReadOnlyList<double>? values = Values;
        if (values is null || values.Count == 0 || Bounds.Width <= 0) return;
        double max = values.Max();
        int peak = HighlightPeak && max > 0 ? IndexOfMax(values) : -1;
        double labelSpace = ShowValues ? 18 : 0;
        var typeface = new Typeface(FontFamily.Default, FontStyle.Normal, FontWeight.SemiBold);

        for (int i = 0; i < values.Count; i++)
        {
            double v = values[i];
            Rect full = BarRect(i, values.Count, v, max, labelSpace, out _);
            double t = Progress(BarStagger * i, BarDuration);
            double grow = Motion.BounceEasing.Ease(t);
            double h = full.Height * grow;
            if (h <= 0.1) continue;
            var rect = new Rect(full.X, Bounds.Height - h, full.Width, h);
            IBrush? brush = v <= 0 ? EmptyFill : i == peak ? PeakFill ?? Fill : Fill;
            if (brush is not null)
            {
                double r = Math.Min(BarRadius, rect.Width / 2);
                context.DrawRectangle(brush, null, new RoundedRect(rect, r, r, 0, 0));
            }
            if (ShowValues && t >= 1 && LabelBrush is { } label)
            {
                var text = new FormattedText(v.ToString("0", CultureInfo.InvariantCulture), CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight, typeface, 12, label);
                context.DrawText(text, new Point(rect.X + (rect.Width - text.Width) / 2, rect.Y - text.Height - 2));
            }
        }
    }

    private static int IndexOfMax(IReadOnlyList<double> values)
    {
        int index = 0;
        for (int i = 1; i < values.Count; i++) if (values[i] > values[index]) index = i;
        return index;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        IReadOnlyList<double>? values = Values;
        if (values is null || values.Count == 0 || Tips is not { } tips) return;
        double x = e.GetPosition(this).X;
        double pitch = (Bounds.Width + BarGap) / values.Count;
        int index = Math.Clamp((int)(x / pitch), 0, values.Count - 1);
        string? tip = index < tips.Count ? tips[index] : null;
        if (!Equals(ToolTip.GetTip(this), tip))
        {
            ToolTip.SetTip(this, tip);
            ToolTip.SetIsOpen(this, false);
            if (tip is not null) ToolTip.SetIsOpen(this, true);
        }
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        ToolTip.SetIsOpen(this, false);
    }
}

/// <summary>
/// 环形图（动效「环形图绘制」）：弧线从 12 点方向顺时针画出，中心数字同步从 0 计数，1200ms。
/// 中心文字由 <see cref="CenterValue"/> 与 <see cref="CenterFormat"/> 组成（如「40%」「532」）。
/// </summary>
public sealed class DonutChart : AnimatedChart
{
    public static readonly StyledProperty<IReadOnlyList<double>?> ValuesProperty =
        AvaloniaProperty.Register<DonutChart, IReadOnlyList<double>?>(nameof(Values));

    public static readonly StyledProperty<IBrush?> Brush1Property = AvaloniaProperty.Register<DonutChart, IBrush?>(nameof(Brush1));
    public static readonly StyledProperty<IBrush?> Brush2Property = AvaloniaProperty.Register<DonutChart, IBrush?>(nameof(Brush2));
    public static readonly StyledProperty<IBrush?> Brush3Property = AvaloniaProperty.Register<DonutChart, IBrush?>(nameof(Brush3));
    public static readonly StyledProperty<IBrush?> Brush4Property = AvaloniaProperty.Register<DonutChart, IBrush?>(nameof(Brush4));

    public static readonly StyledProperty<double> ThicknessProperty =
        AvaloniaProperty.Register<DonutChart, double>(nameof(Thickness), 18);

    public static readonly StyledProperty<double> CenterValueProperty =
        AvaloniaProperty.Register<DonutChart, double>(nameof(CenterValue));

    public static readonly StyledProperty<string> CenterFormatProperty =
        AvaloniaProperty.Register<DonutChart, string>(nameof(CenterFormat), "{0:0}");

    public static readonly StyledProperty<double> FontSizeProperty =
        AvaloniaProperty.Register<DonutChart, double>(nameof(FontSize), 20);

    public static readonly StyledProperty<IBrush?> ForegroundProperty =
        AvaloniaProperty.Register<DonutChart, IBrush?>(nameof(Foreground));

    public static readonly StyledProperty<IBrush?> EmptyBrushProperty =
        AvaloniaProperty.Register<DonutChart, IBrush?>(nameof(EmptyBrush));

    private static readonly TimeSpan Duration = TimeSpan.FromMilliseconds(1200);

    static DonutChart()
    {
        AffectsRender<DonutChart>(Brush1Property, Brush2Property, Brush3Property, Brush4Property, ThicknessProperty, CenterFormatProperty, FontSizeProperty, ForegroundProperty, EmptyBrushProperty);
    }

    /// <summary>各段数值，依次使用 <see cref="Brush1"/>–<see cref="Brush4"/>。</summary>
    public IReadOnlyList<double>? Values { get => GetValue(ValuesProperty); set => SetValue(ValuesProperty, value); }
    public IBrush? Brush1 { get => GetValue(Brush1Property); set => SetValue(Brush1Property, value); }
    public IBrush? Brush2 { get => GetValue(Brush2Property); set => SetValue(Brush2Property, value); }
    public IBrush? Brush3 { get => GetValue(Brush3Property); set => SetValue(Brush3Property, value); }
    public IBrush? Brush4 { get => GetValue(Brush4Property); set => SetValue(Brush4Property, value); }
    public double Thickness { get => GetValue(ThicknessProperty); set => SetValue(ThicknessProperty, value); }
    public double CenterValue { get => GetValue(CenterValueProperty); set => SetValue(CenterValueProperty, value); }
    public string CenterFormat { get => GetValue(CenterFormatProperty); set => SetValue(CenterFormatProperty, value); }
    public double FontSize { get => GetValue(FontSizeProperty); set => SetValue(FontSizeProperty, value); }
    public IBrush? Foreground { get => GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }
    public IBrush? EmptyBrush { get => GetValue(EmptyBrushProperty); set => SetValue(EmptyBrushProperty, value); }

    protected override TimeSpan TotalDuration => Duration;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ValuesProperty || change.Property == CenterValueProperty) Restart();
    }

    public override void Render(DrawingContext context)
    {
        double size = Math.Min(Bounds.Width, Bounds.Height);
        if (size <= 0) return;
        double t = Motion.CountEasing.Ease(Progress(TimeSpan.Zero, Duration));
        var center = new Point(Bounds.Width / 2, Bounds.Height / 2);
        double radius = (size - Thickness) / 2;
        IReadOnlyList<double> values = Values ?? [];
        IBrush?[] brushes = [Brush1, Brush2, Brush3, Brush4];
        double total = values.Sum(v => Math.Max(0, v));

        if (EmptyBrush is { } empty && (total <= 0 || t < 1))
        {
            context.DrawEllipse(null, new Pen(empty, Thickness), center, radius, radius);
        }

        if (total > 0)
        {
            double drawn = 360 * t;
            double angle = 0;
            for (int i = 0; i < values.Count; i++)
            {
                double sweep = Math.Max(0, values[i]) / total * 360;
                double visible = Math.Min(sweep, drawn - angle);
                if (visible > 0.05 && brushes[Math.Min(i, 3)] is { } brush) DrawArc(context, brush, center, radius, angle, visible);
                angle += sweep;
                if (angle >= drawn) break;
            }
        }

        if (Foreground is { } fg)
        {
            string text = string.Format(CultureInfo.InvariantCulture, CenterFormat, CenterValue * t);
            var ft = new FormattedText(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
                new Typeface(FontFamily.Default, FontStyle.Normal, FontWeight.Bold), FontSize, fg);
            context.DrawText(ft, new Point(center.X - ft.Width / 2, center.Y - ft.Height / 2));
        }
    }

    private void DrawArc(DrawingContext context, IBrush brush, Point center, double radius, double startDeg, double sweepDeg)
    {
        var pen = new Pen(brush, Thickness, lineCap: PenLineCap.Flat);
        if (sweepDeg >= 359.95)
        {
            context.DrawEllipse(null, pen, center, radius, radius);
            return;
        }
        Point Polar(double deg)
        {
            double rad = (deg - 90) * Math.PI / 180;
            return new Point(center.X + radius * Math.Cos(rad), center.Y + radius * Math.Sin(rad));
        }
        var geometry = new StreamGeometry();
        using (StreamGeometryContext g = geometry.Open())
        {
            g.BeginFigure(Polar(startDeg), false);
            g.ArcTo(Polar(startDeg + sweepDeg), new Size(radius, radius), 0, sweepDeg > 180, SweepDirection.Clockwise);
            g.EndFigure(false);
        }
        context.DrawGeometry(null, pen, geometry);
    }
}

/// <summary>
/// 折线 + 面积图（详细图表「累计抽数」）：横向虚线网格与左侧刻度数字，折线从左到右画出、面积随之淡入，点上标数值，1200ms。
/// </summary>
public sealed class AreaLineChart : AnimatedChart
{
    public static readonly StyledProperty<IReadOnlyList<double>?> ValuesProperty =
        AvaloniaProperty.Register<AreaLineChart, IReadOnlyList<double>?>(nameof(Values));

    public static readonly StyledProperty<Color> ColorProperty =
        AvaloniaProperty.Register<AreaLineChart, Color>(nameof(Color), Color.Parse("#9A6A14"));

    public static readonly StyledProperty<IBrush?> GridBrushProperty =
        AvaloniaProperty.Register<AreaLineChart, IBrush?>(nameof(GridBrush));

    public static readonly StyledProperty<IBrush?> LabelBrushProperty =
        AvaloniaProperty.Register<AreaLineChart, IBrush?>(nameof(LabelBrush));

    public static readonly StyledProperty<IBrush?> ValueBrushProperty =
        AvaloniaProperty.Register<AreaLineChart, IBrush?>(nameof(ValueBrush));

    public static readonly StyledProperty<IBrush?> DotFillProperty =
        AvaloniaProperty.Register<AreaLineChart, IBrush?>(nameof(DotFill));

    private static readonly TimeSpan Duration = TimeSpan.FromMilliseconds(1200);

    /// <summary>左侧刻度数字所占宽度，图表横轴标签按 <see cref="PlotInset"/> 对齐。</summary>
    public const double PlotInset = 34;

    static AreaLineChart()
    {
        AffectsRender<AreaLineChart>(ColorProperty, GridBrushProperty, LabelBrushProperty, ValueBrushProperty, DotFillProperty);
    }

    public IReadOnlyList<double>? Values { get => GetValue(ValuesProperty); set => SetValue(ValuesProperty, value); }
    public Color Color { get => GetValue(ColorProperty); set => SetValue(ColorProperty, value); }
    public IBrush? GridBrush { get => GetValue(GridBrushProperty); set => SetValue(GridBrushProperty, value); }
    public IBrush? LabelBrush { get => GetValue(LabelBrushProperty); set => SetValue(LabelBrushProperty, value); }
    public IBrush? ValueBrush { get => GetValue(ValueBrushProperty); set => SetValue(ValueBrushProperty, value); }
    public IBrush? DotFill { get => GetValue(DotFillProperty); set => SetValue(DotFillProperty, value); }

    protected override TimeSpan TotalDuration => Duration;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ValuesProperty) Restart();
    }

    private static double NiceStep(double max)
    {
        double raw = Math.Max(1, max / 3);
        double magnitude = Math.Pow(10, Math.Floor(Math.Log10(raw)));
        double normalized = raw / magnitude;
        double nice = normalized <= 1 ? 1 : normalized <= 2 ? 2 : normalized <= 5 ? 5 : 10;
        return nice * magnitude;
    }

    public override void Render(DrawingContext context)
    {
        double w = Bounds.Width, h = Bounds.Height;
        if (w <= 0 || h <= 0) return;
        IReadOnlyList<double> values = Values ?? [];
        double dataMax = values.Count == 0 ? 0 : values.Max();
        double step = NiceStep(dataMax);
        double axisMax = Math.Max(step, Math.Ceiling(dataMax / step) * step);
        const double top = 22, bottom = 4;
        double plotH = h - top - bottom;
        double Y(double v) => top + plotH * (1 - v / axisMax);
        var typeface = new Typeface(FontFamily.Default);

        for (double v = 0; v <= axisMax + 0.001; v += step)
        {
            double y = Math.Round(Y(v)) + 0.5;
            if (GridBrush is { } grid) context.DrawLine(new Pen(grid, 1), new Point(0, y), new Point(w, y));
            if (LabelBrush is { } label)
            {
                var ft = new FormattedText(v.ToString("0", CultureInfo.InvariantCulture), CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight, typeface, 10, label);
                context.DrawText(ft, new Point(0, y - ft.Height - 1));
            }
        }
        if (values.Count == 0) return;

        double t = Motion.CountEasing.Ease(Progress(TimeSpan.Zero, Duration));
        double left = PlotInset, right = w - 16;
        var points = values.Select((v, i) => new Point(
            values.Count == 1 ? (left + right) / 2 : left + (right - left) * i / (values.Count - 1), Y(v))).ToArray();
        double baseY = Y(0);

        using (context.PushClip(new Rect(0, 0, left + (right - left) * t + 12, h)))
        {
            var area = new StreamGeometry();
            using (StreamGeometryContext g = area.Open())
            {
                g.BeginFigure(new Point(points[0].X, baseY), true);
                foreach (Point p in points) g.LineTo(p);
                g.LineTo(new Point(points[^1].X, baseY));
                g.EndFigure(true);
            }
            context.DrawGeometry(new SolidColorBrush(Color, 0.13), null, area);

            var line = new StreamGeometry();
            using (StreamGeometryContext g = line.Open())
            {
                g.BeginFigure(points[0], false);
                foreach (Point p in points.Skip(1)) g.LineTo(p);
                g.EndFigure(false);
            }
            var stroke = new SolidColorBrush(Color);
            context.DrawGeometry(null, new Pen(stroke, 2.5, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round), line);
            var bold = new Typeface(FontFamily.Default, FontStyle.Normal, FontWeight.SemiBold);
            for (int i = 0; i < points.Length; i++)
            {
                Point p = points[i];
                context.DrawEllipse(DotFill ?? Brushes.White, new Pen(stroke, 2), p, 4, 4);
                if (ValueBrush is { } valueBrush && values.Count <= 12)
                {
                    var ft = new FormattedText(values[i].ToString("0", CultureInfo.InvariantCulture), CultureInfo.CurrentCulture,
                        FlowDirection.LeftToRight, bold, 11, valueBrush);
                    context.DrawText(ft, new Point(p.X - ft.Width / 2, p.Y - ft.Height - 5));
                }
            }
        }
    }
}
