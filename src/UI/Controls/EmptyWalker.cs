using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Media;
using WwTool.UI.Theming;

namespace WwTool.UI.Controls;

/// <summary>
/// 空状态小彩蛋（「卡池暂无记录」）：页面停留 3 秒后，60 加载形象沿底部虚线来回慢走，单程约 7 秒（一圈 16 秒），
/// 走动时上下轻跳 3；走到尽头转身，头顶冒出小气泡 <see cref="Bubble"/>（约 1 秒）。减少动画时不出现。
/// 放在卡片底部铺满宽度，不接收指针。
/// </summary>
public sealed class EmptyWalker : Panel
{
    public static readonly StyledProperty<string?> BubbleProperty =
        AvaloniaProperty.Register<EmptyWalker, string?>(nameof(Bubble));

    public string? Bubble { get => GetValue(BubbleProperty); set => SetValue(BubbleProperty, value); }

    private const double Inset = 24;
    private const double Size = 60;
    private const double CycleMs = 16000;
    private const double DelayMs = 3000;

    private readonly Line _ground;
    private readonly Panel _walker;
    private readonly PixelGif _gif;
    private readonly Border _bubble;
    private readonly TextBlock _bubbleText;
    private readonly TranslateTransform _move = new();
    private TimeSpan? _start;
    private bool _running;

    public EmptyWalker()
    {
        IsHitTestVisible = false;
        Height = 90;
        VerticalAlignment = VerticalAlignment.Bottom;
        _ground = new Line
        {
            StrokeThickness = 1,
            StrokeDashArray = [6, 6],
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(Inset, 0, Inset, 22),
        };
        _ground.Bind(Shape.StrokeProperty, this.GetResourceObservable("InputBorderBrush"));
        _gif = new PixelGif { Width = Size, Height = Size };
        _bubbleText = new TextBlock { FontSize = 12 };
        _bubbleText.Bind(TextBlock.TextProperty, this.GetObservable(BubbleProperty));
        _bubbleText.Bind(TextBlock.ForegroundProperty, this.GetResourceObservable("TextSecondaryBrush"));
        _bubble = new Border
        {
            Padding = new Thickness(9, 4),
            CornerRadius = new CornerRadius(9),
            BorderThickness = new Thickness(1),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(-60, -30, -60, 0),
            Opacity = 0,
            Child = _bubbleText,
        };
        _bubble.Bind(Border.BackgroundProperty, this.GetResourceObservable("SurfaceBrush"));
        _bubble.Bind(Border.BorderBrushProperty, this.GetResourceObservable("BorderBrush"));
        _walker = new Panel
        {
            Width = Size,
            Height = Size,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(0, 0, 0, 20),
            RenderTransform = _move,
            Opacity = 0,
            Children = { _gif, _bubble },
        };
        Children.Add(_ground);
        Children.Add(_walker);
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        _ground.EndPoint = new Point(Math.Max(0, e.NewSize.Width - Inset * 2), 0);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _start = null;
        Start();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _running = false;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsVisibleProperty)
        {
            // 每次重新出现都重新等 3 秒。
            _start = null;
            if (IsVisible) Start();
        }
    }

    private void Start()
    {
        if (_running) return;
        _running = true;
        TopLevel.GetTopLevel(this)?.RequestAnimationFrame(Tick);
    }

    private void Tick(TimeSpan now)
    {
        if (!_running || !IsEffectivelyVisible || TopLevel.GetTopLevel(this) is not { } top)
        {
            _running = false;
            return;
        }
        _start ??= now;
        double elapsed = (now - _start.Value).TotalMilliseconds - DelayMs;
        bool reduced = Motion.Reduced;
        _ground.Opacity = reduced ? 0 : 1;
        if (elapsed < 0 || reduced)
        {
            _walker.Opacity = 0;
        }
        else
        {
            double t = elapsed % CycleMs / CycleMs;
            double x0 = Inset, x1 = Math.Max(x0, Bounds.Width - Inset - Size);
            double x = t switch
            {
                < 0.45 => x0 + (x1 - x0) * (t / 0.45),
                < 0.50 => x1,
                < 0.95 => x1 - (x1 - x0) * ((t - 0.50) / 0.45),
                _ => x0,
            };
            // 上下轻跳：每 0.5 秒一次，最高 3。
            double bob = -3 * (1 - Math.Cos(elapsed / 500 * Math.PI * 2)) / 2;
            _move.X = x;
            _move.Y = bob;
            _gif.IsMirrored = t is >= 0.47 and < 0.97;
            // 首圈前 3% 淡入。
            _walker.Opacity = elapsed < CycleMs * 0.03 ? elapsed / (CycleMs * 0.03) : 1;
            _bubble.Opacity = t switch
            {
                < 0.45 => 0,
                < 0.47 => (t - 0.45) / 0.02,
                < 0.52 => 1,
                < 0.55 => 1 - (t - 0.52) / 0.03,
                _ => 0,
            };
        }
        top.RequestAnimationFrame(Tick);
    }
}
