using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace WwTool.UI.Controls;

/// <summary>
/// 默认底纹：40 网格细线 + 右上、左下两团光晕（颜色跟随强调色）。
/// 对应设计稿 radial-gradient(circle at 92% 6%, .20, transparent 34%) 与 (4% 96%, .12, transparent 30%)；
/// 半径按 CSS 规则取圆心到最远角距离的百分比。
/// </summary>
public sealed class BackdropTexture : Control
{
    public static readonly StyledProperty<Color> GridColorProperty =
        AvaloniaProperty.Register<BackdropTexture, Color>(nameof(GridColor), Color.Parse("#785F3C"));

    public static readonly StyledProperty<double> GridOpacityProperty =
        AvaloniaProperty.Register<BackdropTexture, double>(nameof(GridOpacity), 0.075);

    public static readonly StyledProperty<Color> GlowColorProperty =
        AvaloniaProperty.Register<BackdropTexture, Color>(nameof(GlowColor), Color.Parse("#9A6A14"));

    public static readonly StyledProperty<double> GlowOpacityProperty =
        AvaloniaProperty.Register<BackdropTexture, double>(nameof(GlowOpacity), 0.2);

    public static readonly StyledProperty<bool> ShowGridProperty =
        AvaloniaProperty.Register<BackdropTexture, bool>(nameof(ShowGrid), true);

    static BackdropTexture()
    {
        AffectsRender<BackdropTexture>(GridColorProperty, GridOpacityProperty, GlowColorProperty, GlowOpacityProperty, ShowGridProperty);
        IsHitTestVisibleProperty.OverrideDefaultValue<BackdropTexture>(false);
    }

    public Color GridColor { get => GetValue(GridColorProperty); set => SetValue(GridColorProperty, value); }

    public double GridOpacity { get => GetValue(GridOpacityProperty); set => SetValue(GridOpacityProperty, value); }

    public Color GlowColor { get => GetValue(GlowColorProperty); set => SetValue(GlowColorProperty, value); }

    public double GlowOpacity { get => GetValue(GlowOpacityProperty); set => SetValue(GlowOpacityProperty, value); }

    /// <summary>自定义背景图时关掉网格，只留光晕或什么都不画。</summary>
    public bool ShowGrid { get => GetValue(ShowGridProperty); set => SetValue(ShowGridProperty, value); }

    public override void Render(DrawingContext context)
    {
        double w = Bounds.Width, h = Bounds.Height;
        if (w <= 0 || h <= 0) return;

        // 右上暖光（比强调色略亮）与左下强调色光晕。
        Color warm = Mix(GlowColor, Colors.White, 0.28);
        DrawGlow(context, new Point(w * 0.92, h * 0.06), 0.34, warm, GlowOpacity, w, h);
        DrawGlow(context, new Point(w * 0.04, h * 0.96), 0.30, GlowColor, GlowOpacity * 0.6, w, h);

        if (!ShowGrid) return;
        var pen = new Pen(new SolidColorBrush(GridColor, GridOpacity), 1);
        for (double x = 39.5; x < w; x += 40) context.DrawLine(pen, new Point(x, 0), new Point(x, h));
        for (double y = 39.5; y < h; y += 40) context.DrawLine(pen, new Point(0, y), new Point(w, y));
    }

    private static void DrawGlow(DrawingContext context, Point center, double radiusRatio, Color color, double opacity, double w, double h)
    {
        double far = new[] { new Point(0, 0), new Point(w, 0), new Point(0, h), new Point(w, h) }
            .Max(p => Math.Sqrt(Math.Pow(p.X - center.X, 2) + Math.Pow(p.Y - center.Y, 2)));
        double r = far * radiusRatio;
        var brush = new RadialGradientBrush
        {
            Center = new RelativePoint(center, RelativeUnit.Absolute),
            GradientOrigin = new RelativePoint(center, RelativeUnit.Absolute),
            RadiusX = new RelativeScalar(r, RelativeUnit.Absolute),
            RadiusY = new RelativeScalar(r, RelativeUnit.Absolute),
            GradientStops =
            {
                new GradientStop(Color.FromArgb((byte)(255 * opacity), color.R, color.G, color.B), 0),
                new GradientStop(Color.FromArgb(0, color.R, color.G, color.B), 1),
            },
        };
        context.DrawRectangle(brush, null, new Rect(center.X - r, center.Y - r, r * 2, r * 2));
    }

    private static Color Mix(Color a, Color b, double t) => Color.FromRgb(
        (byte)(a.R + (b.R - a.R) * t), (byte)(a.G + (b.G - a.G) * t), (byte)(a.B + (b.B - a.B) * t));
}
