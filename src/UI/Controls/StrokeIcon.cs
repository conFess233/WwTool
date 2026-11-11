using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;

namespace WwTool.UI.Controls;

/// <summary>
/// 线性图标：24 × 24 坐标的几何（<c>Theming/Icons.axaml</c> 的 <c>Icon.*</c>），按控件尺寸缩放，
/// 描边 2、圆角端点与拐角，颜色继承文字前景色。
/// </summary>
public sealed class StrokeIcon : Control
{
    public static readonly StyledProperty<Geometry?> DataProperty =
        AvaloniaProperty.Register<StrokeIcon, Geometry?>(nameof(Data));

    public static readonly StyledProperty<double> StrokeThicknessProperty =
        AvaloniaProperty.Register<StrokeIcon, double>(nameof(StrokeThickness), 2);

    public static readonly StyledProperty<IBrush?> ForegroundProperty =
        TextElement.ForegroundProperty.AddOwner<StrokeIcon>();

    /// <summary>实心填充（播放图标等）；默认只描边。</summary>
    public static readonly StyledProperty<bool> IsFilledProperty =
        AvaloniaProperty.Register<StrokeIcon, bool>(nameof(IsFilled));

    static StrokeIcon()
    {
        AffectsRender<StrokeIcon>(DataProperty, StrokeThicknessProperty, ForegroundProperty, IsFilledProperty);
        WidthProperty.OverrideDefaultValue<StrokeIcon>(18);
        HeightProperty.OverrideDefaultValue<StrokeIcon>(18);
    }

    public Geometry? Data { get => GetValue(DataProperty); set => SetValue(DataProperty, value); }

    public double StrokeThickness { get => GetValue(StrokeThicknessProperty); set => SetValue(StrokeThicknessProperty, value); }

    public IBrush? Foreground { get => GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }

    public bool IsFilled { get => GetValue(IsFilledProperty); set => SetValue(IsFilledProperty, value); }

    public override void Render(DrawingContext context)
    {
        if (Data is null || Foreground is null) return;
        double scale = Math.Min(Bounds.Width, Bounds.Height) / 24;
        if (scale <= 0) return;
        double ox = (Bounds.Width - 24 * scale) / 2, oy = (Bounds.Height - 24 * scale) / 2;
        var pen = new Pen(Foreground, StrokeThickness, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
        using (context.PushTransform(Matrix.CreateScale(scale, scale) * Matrix.CreateTranslation(ox, oy)))
        {
            // 线宽按 24 坐标计，缩放后保持设计比例（18px 图标约 1.5px）。
            context.DrawGeometry(IsFilled ? Foreground : null, pen, Data);
        }
    }
}
