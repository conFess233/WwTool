using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using WwTool.UI.Controls;
using WwTool.UI.Theming;

namespace WwTool.UI.Pages.Home;

/// <summary>
/// 公告横幅（动效「公告轮播」）：高 168（启动器轮播图为整张图片时按 16:9 显示完整画面）、圆角 10；每 4 秒交叉淡入下一张，新一张从 102% 缩小到 100%。
/// 左下角圆点：当前点拉长为 26，并从左到右填充金色表示本张剩余时间。不可见时暂停。
/// </summary>
public sealed class BannerCarousel : Panel
{
    public static readonly StyledProperty<IReadOnlyList<AnnouncementBanner>?> BannersProperty =
        AvaloniaProperty.Register<BannerCarousel, IReadOnlyList<AnnouncementBanner>?>(nameof(Banners));

    /// <summary>点击当前一张时执行，参数为该 <see cref="AnnouncementBanner"/>。</summary>
    public static readonly StyledProperty<System.Windows.Input.ICommand?> CommandProperty =
        AvaloniaProperty.Register<BannerCarousel, System.Windows.Input.ICommand?>(nameof(Command));

    private static readonly TimeSpan SlideTime = TimeSpan.FromSeconds(4);
    private static readonly TimeSpan FadeTime = TimeSpan.FromMilliseconds(480);

    private readonly List<Control> _slides = [];
    private readonly Dots _dots = new();
    private int _index;
    private int _previous = -1;
    private TimeSpan? _slideStart;
    private TimeSpan _now;
    private bool _frameRequested;

    private readonly List<AnnouncementBanner> _banners = [];
    private readonly List<Bitmap> _bitmaps = [];
    private bool _imageMode;

    public BannerCarousel()
    {
        ClipToBounds = true;
        Background = new SolidColorBrush(Color.Parse("#2B2620"));
        _dots.Owner = this;
    }

    public IReadOnlyList<AnnouncementBanner>? Banners { get => GetValue(BannersProperty); set => SetValue(BannersProperty, value); }
    public System.Windows.Input.ICommand? Command { get => GetValue(CommandProperty); set => SetValue(CommandProperty, value); }

    /// <summary>当前显示的一张。</summary>
    private AnnouncementBanner? Current => _index >= 0 && _index < _banners.Count ? _banners[_index] : null;

    protected override Size MeasureOverride(Size availableSize)
    {
        double width = double.IsInfinity(availableSize.Width) ? 390 : availableSize.Width;
        double height = _imageMode ? Math.Round(width * 9 / 16) : 168;
        var size = new Size(width, height);
        foreach (Control child in Children) child.Measure(size);
        return size;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (e.InitialPressMouseButton != MouseButton.Left || Current is not { } banner) return;
        if (Command?.CanExecute(banner) == true) Command.Execute(banner);
    }

    private void UpdateCursor() => Cursor = Current?.Link is not null ? new Cursor(StandardCursorType.Hand) : Cursor.Default;

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
        _banners.Clear();
        foreach (Bitmap old in _bitmaps) old.Dispose();
        _bitmaps.Clear();
        foreach (AnnouncementBanner banner in Banners ?? [])
        {
            Control? slide = banner.LocalImage is { } file ? BuildImageSlide(file) : BuildSlide(banner);
            if (slide is null) continue;
            slide.Opacity = 0;
            _slides.Add(slide);
            _banners.Add(banner);
            Children.Add(slide);
        }
        _imageMode = _banners.Any(b => b.LocalImage is not null);
        InvalidateMeasure();
        Children.Add(_dots);
        _dots.InvalidateMeasure();
        _index = 0;
        _previous = -1;
        _slideStart = null;
        if (_slides.Count > 0) _slides[0].Opacity = 1;
        UpdateCursor();
        RequestFrame();
    }

    /// <summary>启动器轮播图：整张图片铺满（读不了的图片跳过）。</summary>
    private Control? BuildImageSlide(string file)
    {
        try
        {
            using FileStream stream = File.OpenRead(file);
            Bitmap bitmap = Bitmap.DecodeToWidth(stream, 960);
            _bitmaps.Add(bitmap);
            return new Panel
            {
                Background = new SolidColorBrush(Color.Parse("#2B2620")),
                RenderTransformOrigin = RelativePoint.Center,
                Children = { new Image { Source = bitmap, Stretch = Stretch.UniformToFill } },
            };
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException or NotSupportedException)
        {
            return null;
        }
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
            UpdateCursor();
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
