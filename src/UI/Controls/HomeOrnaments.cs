using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace WwTool.UI.Controls;

/// <summary>
/// 首页底纹（开发文档「首页状态画板 → 底纹」）里贴在卡片上的小装饰，只在首页使用。都不参与命中测试。
/// </summary>
public abstract class OrnamentBase : Control
{
    public static readonly StyledProperty<IBrush?> BrushProperty =
        AvaloniaProperty.Register<OrnamentBase, IBrush?>(nameof(Brush));

    static OrnamentBase()
    {
        AffectsRender<OrnamentBase>(BrushProperty);
        IsHitTestVisibleProperty.OverrideDefaultValue<OrnamentBase>(false);
    }

    public IBrush? Brush { get => GetValue(BrushProperty); set => SetValue(BrushProperty, value); }
}

/// <summary>卡片左上与右下的金色角标：14 × 2 横线与 2 × 14 竖线，距边 10。</summary>
public sealed class CornerMarks : OrnamentBase
{
    public override void Render(DrawingContext context)
    {
        if (Brush is null) return;
        double w = Bounds.Width, h = Bounds.Height;
        context.DrawRectangle(Brush, null, new Rect(10, 10, 14, 2));
        context.DrawRectangle(Brush, null, new Rect(10, 10, 2, 14));
        context.DrawRectangle(Brush, null, new Rect(w - 24, h - 12, 14, 2));
        context.DrawRectangle(Brush, null, new Rect(w - 12, h - 24, 2, 14));
    }
}

/// <summary>账号卡右上角的点阵：12 间距、半径 1.3 的圆点，以 (80%, 20%) 为中心向外椭圆渐隐。</summary>
public sealed class DotField : OrnamentBase
{
    public override void Render(DrawingContext context)
    {
        if (Brush is null) return;
        double w = Bounds.Width, h = Bounds.Height;
        double cx = w * 0.8, cy = h * 0.2;
        // CSS 椭圆渐变 farthest-corner：半轴按到最远角的距离等比放大。
        double fx = Math.Max(cx, w - cx), fy = Math.Max(cy, h - cy);
        double rx = fx * Math.Sqrt(2), ry = fy * Math.Sqrt(2);
        for (double y = 6; y < h; y += 12)
        {
            for (double x = 6; x < w; x += 12)
            {
                double dx = (x - cx) / rx, dy = (y - cy) / ry;
                double d = Math.Sqrt(dx * dx + dy * dy);
                double alpha = d <= 0.2 ? 1 : d >= 0.7 ? 0 : 1 - (d - 0.2) / 0.5;
                if (alpha <= 0.02) continue;
                using (context.PushOpacity(alpha))
                {
                    context.DrawEllipse(Brush, null, new Point(x, y), 1.3, 1.3);
                }
            }
        }
    }
}

/// <summary>资源卡顶部 46 高的 135° 斜线纹：1 宽、7 间距。</summary>
public sealed class HatchStrip : OrnamentBase
{
    public override void Render(DrawingContext context)
    {
        if (Brush is null) return;
        double w = Bounds.Width, h = Bounds.Height;
        var pen = new Pen(Brush, 1);
        using (context.PushClip(new Rect(Bounds.Size)))
        {
            // 135°：线从左上往右下排布，每条线方向为 (1, 1)。间距 7 沿法线方向，即横向 7√2。
            double step = 7 * Math.Sqrt(2);
            for (double x = -h; x < w; x += step)
            {
                context.DrawLine(pen, new Point(x, 0), new Point(x + h, h));
            }
        }
    }
}

/// <summary>首页窗口级装饰：十字定位标记、右侧竖排小字、两条金色渐隐短线。坐标按 1440 × 900 定稿换算到当前窗口。</summary>
public sealed class HomeBackdropDecor : OrnamentBase
{
    public static readonly StyledProperty<IBrush?> LineBrushProperty =
        AvaloniaProperty.Register<HomeBackdropDecor, IBrush?>(nameof(LineBrush));

    public static readonly StyledProperty<Color> GoldColorProperty =
        AvaloniaProperty.Register<HomeBackdropDecor, Color>(nameof(GoldColor), Color.Parse("#9A6A14"));

    /// <summary>页头下移量（Windows 为 12，macOS 为 0），十字标记随内容区一起下移。</summary>
    public static readonly StyledProperty<double> TopOffsetProperty =
        AvaloniaProperty.Register<HomeBackdropDecor, double>(nameof(TopOffset));

    static HomeBackdropDecor()
    {
        AffectsRender<HomeBackdropDecor>(LineBrushProperty, GoldColorProperty, TopOffsetProperty);
    }

    public IBrush? LineBrush { get => GetValue(LineBrushProperty); set => SetValue(LineBrushProperty, value); }
    public Color GoldColor { get => GetValue(GoldColorProperty); set => SetValue(GoldColorProperty, value); }
    public double TopOffset { get => GetValue(TopOffsetProperty); set => SetValue(TopOffsetProperty, value); }

    public override void Render(DrawingContext context)
    {
        double w = Bounds.Width, h = Bounds.Height;
        if (w <= 0 || h <= 0) return;
        double top = TopOffset;

        if (Brush is { } cross)
        {
            // 十字：18 × 18、线宽 1.4。位置：左栏四角（左栏右缘在公告栏左侧 9 处）与页头右上。
            double columnRight = w - 40 - 430 - 9;
            DrawCross(context, cross, 40, 120 + top);
            DrawCross(context, cross, columnRight, 120 + top);
            DrawCross(context, cross, 40, h - 8);
            DrawCross(context, cross, columnRight, h - 8);
            DrawCross(context, cross, w - 40, 29 + top);
        }

        if (LineBrush is { } text)
        {
            // 右侧竖排小字：距右 14、垂直居中、字距 6。
            var typeface = new Typeface(new FontFamily("Segoe UI"), FontStyle.Normal, FontWeight.SemiBold);
            const string label = "WUTHERING WAVES TOOLKIT  2.0";
            double x = 0;
            var glyphs = new List<(FormattedText Text, double X)>();
            foreach (char c in label)
            {
                var ft = new FormattedText(c.ToString(), System.Globalization.CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeface, 10, text);
                glyphs.Add((ft, x));
                x += ft.WidthIncludingTrailingWhitespace + 6;
            }
            double length = x - 6;
            using (context.PushTransform(Matrix.CreateRotation(Math.PI / 2) * Matrix.CreateTranslation(w - 14, h / 2 - length / 2)))
            {
                foreach ((FormattedText ft, double gx) in glyphs)
                {
                    context.DrawText(ft, new Point(gx, -10));
                }
            }
        }

        // 金色渐隐短线：左上 180 宽（y 95）与右下 140 宽（距底 80）。
        DrawGoldLine(context, new Rect(0, 95 + top, 180, 6), false);
        DrawGoldLine(context, new Rect(w - 140, h - 80, 140, 6), true);
    }

    private static void DrawCross(DrawingContext context, IBrush brush, double cx, double cy)
    {
        context.DrawRectangle(brush, null, new Rect(cx - 0.7, cy - 9, 1.4, 18));
        context.DrawRectangle(brush, null, new Rect(cx - 9, cy - 0.7, 18, 1.4));
    }

    private void DrawGoldLine(DrawingContext context, Rect rect, bool mirrored)
    {
        Color solid = Color.FromArgb(89, GoldColor.R, GoldColor.G, GoldColor.B);
        Color clear = Color.FromArgb(0, GoldColor.R, GoldColor.G, GoldColor.B);
        var brush = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(mirrored ? 1 : 0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(mirrored ? 0 : 1, 0, RelativeUnit.Relative),
            GradientStops = { new GradientStop(solid, 0), new GradientStop(clear, 1) },
        };
        context.DrawRectangle(brush, null, rect);
    }
}
