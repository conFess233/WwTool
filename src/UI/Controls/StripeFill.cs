using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace WwTool.UI.Controls;

/// <summary>
/// 斜条纹底（CSS repeating-linear-gradient(135deg, A 0 w, B w 2w) 的等价）：先铺 <see cref="Background"/>，
/// 再画「/」方向、周期 <see cref="Period"/> 的 <see cref="Stripe"/> 条纹。用于精简视图「未出金」磁贴的头像区。
/// </summary>
public sealed class StripeFill : Control
{
    public static readonly StyledProperty<IBrush?> BackgroundProperty =
        AvaloniaProperty.Register<StripeFill, IBrush?>(nameof(Background));

    public static readonly StyledProperty<IBrush?> StripeProperty =
        AvaloniaProperty.Register<StripeFill, IBrush?>(nameof(Stripe));

    public static readonly StyledProperty<double> PeriodProperty =
        AvaloniaProperty.Register<StripeFill, double>(nameof(Period), 12);

    public IBrush? Background { get => GetValue(BackgroundProperty); set => SetValue(BackgroundProperty, value); }
    public IBrush? Stripe { get => GetValue(StripeProperty); set => SetValue(StripeProperty, value); }
    public double Period { get => GetValue(PeriodProperty); set => SetValue(PeriodProperty, value); }

    static StripeFill()
    {
        AffectsRender<StripeFill>(BackgroundProperty, StripeProperty, PeriodProperty);
        ClipToBoundsProperty.OverrideDefaultValue<StripeFill>(true);
    }

    public override void Render(DrawingContext context)
    {
        double w = Bounds.Width, h = Bounds.Height;
        if (w <= 0 || h <= 0) return;
        var rect = new Rect(0, 0, w, h);
        if (Background is { } bg) context.DrawRectangle(bg, null, rect);
        if (Stripe is not { } stripe) return;

        // 条纹沿 x + y = c 方向；垂直周期 Period，对应 x 轴上的步长 Period·√2，条纹宽为一半。
        double step = Math.Max(2, Period) * Math.Sqrt(2);
        double band = step / 2;
        var geo = new StreamGeometry();
        using (var g = geo.Open())
        {
            for (double c = 0; c < w + h + step; c += step)
            {
                g.BeginFigure(new Point(c, 0), true);
                g.LineTo(new Point(c + band, 0));
                g.LineTo(new Point(c + band - h, h));
                g.LineTo(new Point(c - h, h));
                g.EndFigure(true);
            }
        }
        using (context.PushClip(rect))
        {
            context.DrawGeometry(stripe, null, geo);
        }
    }
}
