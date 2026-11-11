using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace WwTool.UI.Controls;

/// <summary>
/// 逐笔画出的对勾（动效「刷新完成」）：<see cref="Progress"/> 0→1 时沿路径画出，配合 Transitions 实现 480ms 描线。
/// 路径为 24×24 坐标系的 M5 12 l5 5 9-10，按控件大小缩放。
/// </summary>
public sealed class CheckMark : Control
{
    public static readonly StyledProperty<double> ProgressProperty =
        AvaloniaProperty.Register<CheckMark, double>(nameof(Progress));

    public static readonly StyledProperty<IBrush?> StrokeProperty =
        AvaloniaProperty.Register<CheckMark, IBrush?>(nameof(Stroke));

    public static readonly StyledProperty<double> StrokeThicknessProperty =
        AvaloniaProperty.Register<CheckMark, double>(nameof(StrokeThickness), 2.8);

    public double Progress { get => GetValue(ProgressProperty); set => SetValue(ProgressProperty, value); }
    public IBrush? Stroke { get => GetValue(StrokeProperty); set => SetValue(StrokeProperty, value); }
    public double StrokeThickness { get => GetValue(StrokeThicknessProperty); set => SetValue(StrokeThicknessProperty, value); }

    private static readonly Point[] Points = [new(5, 12), new(10, 17), new(19, 7)];

    static CheckMark() => AffectsRender<CheckMark>(ProgressProperty, StrokeProperty, StrokeThicknessProperty);

    public override void Render(DrawingContext context)
    {
        double p = Math.Clamp(Progress, 0, 1);
        if (p <= 0 || Stroke is null) return;
        double scale = Math.Min(Bounds.Width, Bounds.Height) / 24;
        var pts = Points.Select(x => new Point(x.X * scale, x.Y * scale)).ToArray();
        double total = 0;
        for (int i = 1; i < pts.Length; i++) total += Distance(pts[i - 1], pts[i]);
        double remaining = total * p;
        var geometry = new StreamGeometry();
        using (StreamGeometryContext g = geometry.Open())
        {
            g.BeginFigure(pts[0], false);
            for (int i = 1; i < pts.Length && remaining > 0; i++)
            {
                double seg = Distance(pts[i - 1], pts[i]);
                if (remaining >= seg)
                {
                    g.LineTo(pts[i]);
                    remaining -= seg;
                }
                else
                {
                    double f = remaining / seg;
                    g.LineTo(new Point(pts[i - 1].X + (pts[i].X - pts[i - 1].X) * f, pts[i - 1].Y + (pts[i].Y - pts[i - 1].Y) * f));
                    remaining = 0;
                }
            }
        }
        context.DrawGeometry(null, new Pen(Stroke, StrokeThickness, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round), geometry);
    }

    private static double Distance(Point a, Point b) => Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));
}
