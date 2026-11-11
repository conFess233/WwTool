using System.Globalization;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media;

namespace WwTool.UI.Controls;

/// <summary>
/// 按住确认的危险按钮（动效「按住清空」）：按住 <see cref="HoldDuration"/> 期间从左到右填充 <see cref="FillBrush"/>，
/// 被填充部分的文字变为 <see cref="FillForeground"/>；中途松开即回退取消；填满后执行 <see cref="Command"/>，
/// 并显示对勾与 <see cref="DoneText"/> 一段时间后复原。键盘按住空格同样生效。
/// </summary>
public sealed class HoldButton : TemplatedControl
{
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<HoldButton, string?>(nameof(Text));

    public static readonly StyledProperty<string?> DoneTextProperty =
        AvaloniaProperty.Register<HoldButton, string?>(nameof(DoneText));

    public static readonly StyledProperty<ICommand?> CommandProperty =
        AvaloniaProperty.Register<HoldButton, ICommand?>(nameof(Command));

    public static readonly StyledProperty<TimeSpan> HoldDurationProperty =
        AvaloniaProperty.Register<HoldButton, TimeSpan>(nameof(HoldDuration), TimeSpan.FromSeconds(1.5));

    public static readonly StyledProperty<IBrush?> FillBrushProperty =
        AvaloniaProperty.Register<HoldButton, IBrush?>(nameof(FillBrush));

    public static readonly StyledProperty<IBrush?> FillForegroundProperty =
        AvaloniaProperty.Register<HoldButton, IBrush?>(nameof(FillForeground), Brushes.White);

    public string? Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }
    public string? DoneText { get => GetValue(DoneTextProperty); set => SetValue(DoneTextProperty, value); }
    public ICommand? Command { get => GetValue(CommandProperty); set => SetValue(CommandProperty, value); }
    public TimeSpan HoldDuration { get => GetValue(HoldDurationProperty); set => SetValue(HoldDurationProperty, value); }
    public IBrush? FillBrush { get => GetValue(FillBrushProperty); set => SetValue(FillBrushProperty, value); }
    public IBrush? FillForeground { get => GetValue(FillForegroundProperty); set => SetValue(FillForegroundProperty, value); }

    /// <summary>松手后回退速度：约 250ms 退完。</summary>
    private const double ReleaseMs = 250;
    private const double DoneMs = 1800;

    private double _progress;
    private bool _holding;
    private bool _done;
    private bool _ticking;
    private TimeSpan? _last;
    private double _doneElapsed;

    static HoldButton()
    {
        FocusableProperty.OverrideDefaultValue<HoldButton>(true);
        CursorProperty.OverrideDefaultValue<HoldButton>(new Cursor(StandardCursorType.Hand));
        AffectsRender<HoldButton>(TextProperty, DoneTextProperty, FillBrushProperty, FillForegroundProperty,
            BackgroundProperty, BorderBrushProperty, ForegroundProperty);
        AffectsMeasure<HoldButton>(TextProperty, DoneTextProperty, FontSizeProperty, PaddingProperty);
    }

    public HoldButton()
    {
        ClipToBounds = true;
    }

    /// <summary>当前填充进度（0…1），供测试与自动化读取。</summary>
    public double Progress => _progress;

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed || !IsEffectivelyEnabled) return;
        e.Pointer.Capture(this);
        Begin();
        e.Handled = true;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        e.Pointer.Capture(null);
        _holding = false;
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        _holding = false;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key is Key.Space or Key.Enter && IsEffectivelyEnabled)
        {
            if (!_holding) Begin();
            e.Handled = true;
        }
    }

    protected override void OnKeyUp(KeyEventArgs e)
    {
        base.OnKeyUp(e);
        if (e.Key is Key.Space or Key.Enter) _holding = false;
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        _holding = false;
    }

    private void Begin()
    {
        if (_done) return;
        _holding = true;
        if (Command?.CanExecute(null) == false) { _holding = false; return; }
        StartTicking();
    }

    private void StartTicking()
    {
        if (_ticking) return;
        _ticking = true;
        _last = null;
        TopLevel.GetTopLevel(this)?.RequestAnimationFrame(Tick);
    }

    private void Tick(TimeSpan now)
    {
        double dt = _last is { } last ? (now - last).TotalMilliseconds : 16;
        _last = now;
        dt = Math.Clamp(dt, 0, 64);

        if (_done)
        {
            _doneElapsed += dt;
            if (_doneElapsed >= DoneMs)
            {
                _done = false;
                _progress = 0;
            }
        }
        else if (_holding)
        {
            double total = Math.Max(1, HoldDuration.TotalMilliseconds);
            _progress = Math.Min(1, _progress + dt / total);
            if (_progress >= 1)
            {
                _holding = false;
                _done = true;
                _doneElapsed = 0;
                if (Command?.CanExecute(null) == true) Command.Execute(null);
            }
        }
        else
        {
            _progress = Math.Max(0, _progress - dt / ReleaseMs);
        }

        InvalidateVisual();
        if (_done || _holding || _progress > 0)
        {
            TopLevel.GetTopLevel(this)?.RequestAnimationFrame(Tick);
        }
        else
        {
            _ticking = false;
        }
    }

    private FormattedText Format(string text, IBrush? brush) => new(
        text, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
        new Typeface(FontFamily, FontStyle, FontWeight), FontSize, brush ?? Foreground);

    protected override Size MeasureOverride(Size availableSize)
    {
        var a = Format(Text ?? string.Empty, null);
        var b = Format(DoneText ?? string.Empty, null);
        double w = Math.Max(a.Width, b.Width + 20) + Padding.Left + Padding.Right;
        double h = Math.Max(a.Height, b.Height) + Padding.Top + Padding.Bottom;
        return new Size(Math.Max(w, MinWidth), Math.Max(h, MinHeight));
    }

    public override void Render(DrawingContext context)
    {
        var bounds = new Rect(Bounds.Size);
        double radius = CornerRadius.TopLeft;
        var outer = new RoundedRect(bounds, radius);
        if (Background is { } bg) context.DrawRectangle(bg, null, outer);

        // 填充：已完成时满格。
        double fill = _done ? 1 : _progress;
        if (fill > 0 && FillBrush is { } fillBrush)
        {
            using (context.PushClip(outer))
            {
                context.DrawRectangle(fillBrush, null, new Rect(0, 0, bounds.Width * fill, bounds.Height));
            }
        }

        string label = _done ? DoneText ?? string.Empty : Text ?? string.Empty;
        var normal = Format(label, Foreground);
        double checkWidth = _done ? 18 : 0;
        double x = (bounds.Width - normal.Width - checkWidth) / 2 + checkWidth;
        double y = (bounds.Height - normal.Height) / 2;

        // 未填充部分：原文字色。
        double split = bounds.Width * fill;
        if (split < bounds.Width)
        {
            using (context.PushClip(new Rect(split, 0, bounds.Width - split, bounds.Height)))
            {
                context.DrawText(normal, new Point(x, y));
            }
        }
        // 填充部分：文字变白。
        if (split > 0)
        {
            var onFill = Format(label, FillForeground);
            using (context.PushClip(new Rect(0, 0, split, bounds.Height)))
            {
                context.DrawText(onFill, new Point(x, y));
                if (_done)
                {
                    var pen = new Pen(FillForeground, 2, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
                    double cx = x - checkWidth, cy = bounds.Height / 2;
                    var geo = new StreamGeometry();
                    using (var g = geo.Open())
                    {
                        g.BeginFigure(new Point(cx + 1, cy), false);
                        g.LineTo(new Point(cx + 5, cy + 4));
                        g.LineTo(new Point(cx + 12, cy - 4));
                    }
                    context.DrawGeometry(null, pen, geo);
                }
            }
        }

        if (BorderBrush is { } border && BorderThickness.Left > 0)
        {
            double t = BorderThickness.Left;
            context.DrawRectangle(null, new Pen(border, t), new RoundedRect(bounds.Deflate(t / 2), Math.Max(0, radius - t / 2)));
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsEnabledProperty && !IsEffectivelyEnabled) _holding = false;
    }
}
