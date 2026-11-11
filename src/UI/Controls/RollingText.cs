using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using WwTool.UI.Theming;

namespace WwTool.UI.Controls;

/// <summary>
/// 逐字滚动的文字（动效「倒计时跳动」「数据刷新闪光」「账号切换」）：
/// 文字变化时只有变化的字符上滚换新（旧字向上移出、新字从下方进入）；
/// <see cref="FlashOnChange"/> 为 true 时同时闪一下淡金底（45% → 0，约 1.7 秒）。
/// 数字按等宽绘制，跳动时不左右晃。首次显示不播放。减少动画时直接换字、不闪。
/// </summary>
public sealed class RollingText : Control
{
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<RollingText, string?>(nameof(Text));

    public static readonly StyledProperty<double> FontSizeProperty =
        TextElement.FontSizeProperty.AddOwner<RollingText>();

    public static readonly StyledProperty<FontWeight> FontWeightProperty =
        TextElement.FontWeightProperty.AddOwner<RollingText>();

    public static readonly StyledProperty<FontFamily> FontFamilyProperty =
        TextElement.FontFamilyProperty.AddOwner<RollingText>();

    public static readonly StyledProperty<IBrush?> ForegroundProperty =
        TextElement.ForegroundProperty.AddOwner<RollingText>();

    public static readonly StyledProperty<double> LetterSpacingProperty =
        AvaloniaProperty.Register<RollingText, double>(nameof(LetterSpacing));

    public static readonly StyledProperty<bool> FlashOnChangeProperty =
        AvaloniaProperty.Register<RollingText, bool>(nameof(FlashOnChange));

    public static readonly StyledProperty<IBrush?> FlashBrushProperty =
        AvaloniaProperty.Register<RollingText, IBrush?>(nameof(FlashBrush));

    public static readonly StyledProperty<TimeSpan> RollDurationProperty =
        AvaloniaProperty.Register<RollingText, TimeSpan>(nameof(RollDuration), TimeSpan.FromMilliseconds(420));

    /// <summary>首次显示时数字从 0 计数到目标值（动效「数字滚动」，900ms，先快后慢）。</summary>
    public static readonly StyledProperty<bool> CountUpProperty =
        AvaloniaProperty.Register<RollingText, bool>(nameof(CountUp));

    private static readonly TimeSpan FlashDuration = TimeSpan.FromMilliseconds(1700);
    private static readonly TimeSpan CountDuration = TimeSpan.FromMilliseconds(900);
    private static readonly System.Text.RegularExpressions.Regex NumberPattern =
        new(@"^(\D*?)(\d[\d,]*(?:\.\d+)?)(\D*)$", System.Text.RegularExpressions.RegexOptions.Compiled);

    static RollingText()
    {
        AffectsMeasure<RollingText>(TextProperty, FontSizeProperty, FontWeightProperty, FontFamilyProperty, LetterSpacingProperty);
        AffectsRender<RollingText>(ForegroundProperty);
    }

    public string? Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }
    public double FontSize { get => GetValue(FontSizeProperty); set => SetValue(FontSizeProperty, value); }
    public FontWeight FontWeight { get => GetValue(FontWeightProperty); set => SetValue(FontWeightProperty, value); }
    public FontFamily FontFamily { get => GetValue(FontFamilyProperty); set => SetValue(FontFamilyProperty, value); }
    public IBrush? Foreground { get => GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }
    public double LetterSpacing { get => GetValue(LetterSpacingProperty); set => SetValue(LetterSpacingProperty, value); }
    public bool FlashOnChange { get => GetValue(FlashOnChangeProperty); set => SetValue(FlashOnChangeProperty, value); }
    public IBrush? FlashBrush { get => GetValue(FlashBrushProperty); set => SetValue(FlashBrushProperty, value); }
    public TimeSpan RollDuration { get => GetValue(RollDurationProperty); set => SetValue(RollDurationProperty, value); }
    public bool CountUp { get => GetValue(CountUpProperty); set => SetValue(CountUpProperty, value); }

    // 计数动画：前后缀、目标值、小数位与是否带千分位。
    private (string Prefix, double Target, int Decimals, bool Grouped, string Suffix)? _count;
    private TimeSpan? _countStart;
    private bool _countPending;

    private sealed class Slot
    {
        public string Current = string.Empty;
        public string? Previous;
        public TimeSpan? Start;
        public bool Pending;
        public double Progress = 1;
    }

    private readonly List<Slot> _slots = [];
    private readonly Dictionary<string, FormattedText> _glyphs = [];
    private double _digitWidth;
    private double _lineHeight;
    private bool _shown;
    private bool _frameRequested;
    private bool _flashPending;
    private TimeSpan? _flashStart;
    private double _flash;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TextProperty)
        {
            UpdateSlots(change.GetOldValue<string?>(), change.GetNewValue<string?>());
        }
        else if (change.Property == FontSizeProperty || change.Property == FontWeightProperty
                 || change.Property == FontFamilyProperty || change.Property == ForegroundProperty)
        {
            _glyphs.Clear();
        }
    }

    private void UpdateSlots(string? oldText, string? newText)
    {
        if (_count is not null)
        {
            // 计数中数据变了：直接停在新值。
            _count = null;
            _countStart = null;
            _slots.Clear();
            foreach (string c in Split(newText)) _slots.Add(new Slot { Current = c });
            InvalidateMeasure();
            InvalidateVisual();
            return;
        }
        string[] next = Split(newText);
        bool animate = _shown && !Motion.Reduced && TopLevel.GetTopLevel(this) is not null && oldText != newText;
        if (!animate)
        {
            _slots.Clear();
            foreach (string c in next) _slots.Add(new Slot { Current = c });
            InvalidateVisual();
            return;
        }

        bool sameShape = next.Length == _slots.Count;
        if (!sameShape)
        {
            // 长度变了：整段一起换，按右对齐把旧字符对到新位置上。
            var old = _slots.Select(s => s.Current).ToArray();
            _slots.Clear();
            for (int i = 0; i < next.Length; i++)
            {
                int j = i - (next.Length - old.Length);
                string? previous = j >= 0 && j < old.Length ? old[j] : string.Empty;
                _slots.Add(new Slot { Current = next[i], Previous = previous, Pending = true, Progress = 0 });
            }
        }
        else
        {
            for (int i = 0; i < next.Length; i++)
            {
                Slot slot = _slots[i];
                if (slot.Current == next[i]) continue;
                slot.Previous = slot.Current;
                slot.Current = next[i];
                slot.Pending = true;
                slot.Progress = 0;
            }
        }

        if (FlashOnChange)
        {
            _flashPending = true;
            _flash = 0.45;
        }
        RequestFrame();
        InvalidateVisual();
    }

    private static string[] Split(string? text)
    {
        if (string.IsNullOrEmpty(text)) return [];
        var parts = new List<string>();
        TextElementEnumerator e = StringInfo.GetTextElementEnumerator(text);
        while (e.MoveNext()) parts.Add(e.GetTextElement());
        return parts.ToArray();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (!_shown && CountUp && !Motion.Reduced && Text is { } text
            && NumberPattern.Match(text) is { Success: true } m
            && double.TryParse(m.Groups[2].Value.Replace(",", "", StringComparison.Ordinal), NumberStyles.Float, CultureInfo.InvariantCulture, out double target)
            && target > 0)
        {
            string digits = m.Groups[2].Value;
            int dot = digits.IndexOf('.', StringComparison.Ordinal);
            _count = (m.Groups[1].Value, target, dot < 0 ? 0 : digits.Length - dot - 1, digits.Contains(',', StringComparison.Ordinal), m.Groups[3].Value);
            _countPending = true;
            SetCountText(0);
            RequestFrame();
        }
        _shown = true;
    }

    private void SetCountText(double value)
    {
        if (_count is not { } c) return;
        string format = (c.Grouped ? "#,0" : "0") + (c.Decimals > 0 ? "." + new string('0', c.Decimals) : string.Empty);
        string text = c.Prefix + value.ToString(format, CultureInfo.InvariantCulture) + c.Suffix;
        _slots.Clear();
        foreach (string part in Split(text)) _slots.Add(new Slot { Current = part });
        InvalidateMeasure();
        InvalidateVisual();
    }

    private void RequestFrame()
    {
        if (_frameRequested || TopLevel.GetTopLevel(this) is not { } top) return;
        _frameRequested = true;
        top.RequestAnimationFrame(OnFrame);
    }

    private void OnFrame(TimeSpan time)
    {
        _frameRequested = false;
        bool more = false;
        double duration = Math.Max(1, RollDuration.TotalMilliseconds);
        foreach (Slot slot in _slots)
        {
            if (slot.Pending)
            {
                slot.Start = time;
                slot.Pending = false;
            }
            if (slot.Start is not { } start) continue;
            double t = Math.Clamp((time - start).TotalMilliseconds / duration, 0, 1);
            slot.Progress = Motion.StandardEasing.Ease(t);
            if (t >= 1)
            {
                slot.Start = null;
                slot.Previous = null;
                slot.Progress = 1;
            }
            else
            {
                more = true;
            }
        }

        if (_count is { } count)
        {
            if (_countPending)
            {
                _countStart = time;
                _countPending = false;
            }
            double t = _countStart is { } cs ? Math.Clamp((time - cs).TotalMilliseconds / CountDuration.TotalMilliseconds, 0, 1) : 0;
            if (t >= 1)
            {
                _count = null;
                _countStart = null;
                _slots.Clear();
                foreach (string part in Split(Text)) _slots.Add(new Slot { Current = part });
                InvalidateMeasure();
            }
            else
            {
                SetCountText(count.Target * Motion.CountEasing.Ease(t));
                more = true;
            }
        }

        if (_flashPending)
        {
            _flashStart = time;
            _flashPending = false;
        }
        if (_flashStart is { } fs)
        {
            double t = Math.Clamp((time - fs).TotalMilliseconds / FlashDuration.TotalMilliseconds, 0, 1);
            _flash = 0.45 * (1 - Motion.SmoothEasing.Ease(t));
            if (t >= 1)
            {
                _flashStart = null;
                _flash = 0;
            }
            else
            {
                more = true;
            }
        }

        InvalidateVisual();
        if (more) RequestFrame();
    }

    private FormattedText Glyph(string text)
    {
        if (_glyphs.TryGetValue(text, out FormattedText? glyph)) return glyph;
        var typeface = new Typeface(FontFamily, FontStyle.Normal, FontWeight);
        glyph = new FormattedText(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, typeface,
            FontSize > 0 ? FontSize : 14, Foreground ?? Brushes.Black);
        _glyphs[text] = glyph;
        return glyph;
    }

    private void EnsureMetrics()
    {
        FormattedText zero = Glyph("0");
        _digitWidth = Enumerable.Range(0, 10).Max(d => Glyph(d.ToString(CultureInfo.InvariantCulture)).WidthIncludingTrailingWhitespace);
        _lineHeight = zero.Height;
    }

    private double WidthOf(string text) =>
        text.Length == 1 && char.IsAsciiDigit(text[0]) ? _digitWidth : Glyph(text).WidthIncludingTrailingWhitespace;

    protected override Size MeasureOverride(Size availableSize)
    {
        EnsureMetrics();
        double width = 0;
        for (int i = 0; i < _slots.Count; i++)
        {
            width += WidthOf(_slots[i].Current);
            if (i > 0) width += LetterSpacing;
        }
        return new Size(Math.Max(0, width), _lineHeight);
    }

    public override void Render(DrawingContext context)
    {
        if (_slots.Count == 0) return;
        EnsureMetrics();
        double height = _lineHeight;

        if (_flash > 0.001 && FlashBrush is { } flash)
        {
            using (context.PushOpacity(_flash))
            {
                context.DrawRectangle(flash, null, new RoundedRect(new Rect(-4, 0, Bounds.Width + 8, height), 4));
            }
        }

        using (context.PushClip(new Rect(-2, 0, Bounds.Width + 4, height)))
        {
            double x = 0;
            foreach (Slot slot in _slots)
            {
                double w = WidthOf(slot.Current);
                if (slot.Previous is { } previous && slot.Progress < 1)
                {
                    double p = slot.Progress;
                    DrawCentered(context, previous, x, w, -height * p, 1 - p);
                    DrawCentered(context, slot.Current, x, w, height * (1 - p), p);
                }
                else
                {
                    DrawCentered(context, slot.Current, x, w, 0, 1);
                }
                x += w + LetterSpacing;
            }
        }
    }

    private void DrawCentered(DrawingContext context, string text, double x, double slotWidth, double y, double opacity)
    {
        if (text.Length == 0 || opacity <= 0.01) return;
        FormattedText glyph = Glyph(text);
        double offset = (slotWidth - glyph.WidthIncludingTrailingWhitespace) / 2;
        using (context.PushOpacity(opacity))
        {
            context.DrawText(glyph, new Point(x + offset, y));
        }
    }
}
