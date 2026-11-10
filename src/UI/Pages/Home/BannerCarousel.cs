using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using WwTool.UI.Controls;
using WwTool.UI.Theming;

namespace WwTool.UI.Pages.Home;

/// <summary>
/// 公告横幅（动效「公告轮播」）：高 168、圆角 10；每 4 秒交叉淡入下一张，新一张从 102% 缩小到 100%。
/// 左下角圆点：当前点拉长为 26，并从左到右填充金色表示本张剩余时间。不可见时暂停。
/// </summary>
public sealed class BannerCarousel : Panel
{
    public static readonly StyledProperty<IReadOnlyList<AnnouncementBanner>?> BannersProperty =
        AvaloniaProperty.Register<BannerCarousel, IReadOnlyList<AnnouncementBanner>?>(nameof(Banners));

    private static readonly TimeSpan SlideTime = TimeSpan.FromSeconds(4);
    private static readonly TimeSpan FadeTime = TimeSpan.FromMilliseconds(480);

    private readonly List<Control> _slides = [];
    private readonly Dots _dots = new();
    private int _index;
    private int _previous = -1;
    private TimeSpan? _slideStart;
    private TimeSpan _now;
    private bool _frameRequested;

    public BannerCarousel()
    {
        ClipToBounds = true;
        Height = 168;
        Background = new SolidColorBrush(Color.Parse("#2B2620"));
        _dots.Owner = this;
    }

    public IReadOnlyList<AnnouncementBanner>? Banners { get => GetValue(BannersProperty); set => SetValue(BannersProperty, value); }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == BannersProperty) Rebuild();
        else if (change.Property == BoundsProperty && Bounds.Width > 0) ClipTo(Bounds.Size);
    }

    private void ClipTo(Size size) => Clip = new RectangleGeometry(new Rect(size), 10, 10);

    private void Rebuild()
    {
        Children.Clear();
        _slides.Clear();
        foreach (AnnouncementBanner banner in Banners ?? [])
        {
            Control slide = BuildSlide(banner);
            slide.Opacity = 0;
            _slides.Add(slide);
            Children.Add(slide);
        }
        Children.Add(_dots);
        _dots.InvalidateMeasure();
        _index = 0;
        _previous = -1;
        _slideStart = null;
        if (_slides.Count > 0) _slides[0].Opacity = 1;
        RequestFrame();
    }

    private static Control BuildSlide(AnnouncementBanner banner)
    {
        IReadOnlyList<Color> colors = banner.Gradient;
        var gradient = new LinearGradientBrush
        {
            // CSS 120°：从左上偏左往右下。
            StartPoint = new RelativePoint(0, 0.21, RelativeUnit.Relative),
            EndPoint = new RelativePoint(1, 0.79, RelativeUnit.Relative),
        };
        for (int i = 0; i < colors.Count; i++)
        {
            double offset = colors.Count == 3 && i == 1 ? 0.55 : i / (double)(colors.Count - 1);
            gradient.GradientStops.Add(new GradientStop(colors[i], offset));
        }

        var root = new Panel { Background = gradient, RenderTransformOrigin = RelativePoint.Center };
        if (banner.ImagePath is { } path)
        {
            var image = new CatalogImage
            {
                Path = path,
                DecodeWidth = 340,
                Stretch = Stretch.UniformToFill,
                Width = 170,
                Height = 170,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 4, -6, 0),
                Opacity = 0.95,
                OpacityMask = new RadialGradientBrush
                {
                    GradientStops =
                    {
                        new GradientStop(Colors.Black, 0.55 / 0.7071),
                        new GradientStop(Colors.Transparent, Math.Min(1, 0.72 / 0.7071)),
                    },
                },
            };
            root.Children.Add(image);
        }

        var text = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(18, 16, 18, 26),
            Spacing = 4,
        };
        text.Children.Add(new TextBlock { Text = banner.Status, FontSize = 11, FontWeight = FontWeight.Bold, LetterSpacing = 1, Foreground = new SolidColorBrush(Color.Parse("#F5D27A")) });
        text.Children.Add(new TextBlock { Text = banner.Title, FontSize = 18, FontWeight = FontWeight.Bold, Foreground = Brushes.White, TextTrimming = TextTrimming.CharacterEllipsis });
        text.Children.Add(new TextBlock { Text = banner.DateText, FontSize = 12, Foreground = new SolidColorBrush(Color.Parse("#E9DCC2")) });
        root.Children.Add(text);
        return root;
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _slideStart = null;
        RequestFrame();
    }

    private void RequestFrame()
    {
        if (_frameRequested || _slides.Count == 0 || TopLevel.GetTopLevel(this) is not { } top) return;
        _frameRequested = true;
        top.RequestAnimationFrame(OnFrame);
    }

    private void OnFrame(TimeSpan time)
    {
        _frameRequested = false;
        _now = time;
        if (_slides.Count == 0) return;
        _slideStart ??= time;
        TimeSpan elapsed = time - _slideStart.Value;

        if (_slides.Count > 1 && elapsed >= SlideTime)
        {
            _previous = _index;
            _index = (_index + 1) % _slides.Count;
            _slideStart = time;
            elapsed = TimeSpan.Zero;
        }

        double t = Math.Clamp(elapsed.TotalMilliseconds / FadeTime.TotalMilliseconds, 0, 1);
        double eased = Motion.StandardEasing.Ease(t);
        for (int i = 0; i < _slides.Count; i++)
        {
            Control slide = _slides[i];
            if (i == _index)
            {
                slide.Opacity = _previous < 0 ? 1 : eased;
                double scale = Motion.Reduced || _previous < 0 ? 1 : 1.02 - 0.02 * eased;
                slide.RenderTransform = new ScaleTransform(scale, scale);
                slide.ZIndex = 1;
            }
            else if (i == _previous)
            {
                slide.Opacity = 1 - eased;
                slide.ZIndex = 0;
            }
            else
            {
                slide.Opacity = 0;
            }
        }

        _dots.Progress = Math.Clamp(elapsed.TotalMilliseconds / SlideTime.TotalMilliseconds, 0, 1);
        _dots.InvalidateVisual();
        if (IsEffectivelyVisible) RequestFrame();
    }

    /// <summary>左下角圆点：8 × 6，当前点 26 宽并按进度填充金色。</summary>
    private sealed class Dots : Control
    {
        public BannerCarousel? Owner;
        public double Progress;

        public Dots()
        {
            HorizontalAlignment = HorizontalAlignment.Left;
            VerticalAlignment = VerticalAlignment.Bottom;
            Margin = new Thickness(18, 0, 0, 12);
            Height = 6;
            ZIndex = 2;
            IsHitTestVisible = false;
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            int count = Owner?._slides.Count ?? 0;
            return count <= 1 ? default : new Size(26 + (count - 1) * 8 + (count - 1) * 6, 6);
        }

        public override void Render(DrawingContext context)
        {
            if (Owner is not { } owner || owner._slides.Count <= 1) return;
            var track = new SolidColorBrush(Color.FromArgb(89, 255, 255, 255));
            var gold = new SolidColorBrush(Color.Parse("#F5D27A"));
            double x = 0;
            for (int i = 0; i < owner._slides.Count; i++)
            {
                bool current = i == owner._index;
                double w = current ? 26 : 8;
                var rect = new RoundedRect(new Rect(x, 0, w, 6), 3);
                context.DrawRectangle(track, null, rect);
                if (current && Progress > 0)
                {
                    using (context.PushClip(rect))
                    {
                        context.DrawRectangle(gold, null, new Rect(x, 0, w * Progress, 6));
                    }
                }
                x += w + 6;
            }
        }
    }
}
