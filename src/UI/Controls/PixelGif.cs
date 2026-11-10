using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using SkiaSharp;
using WwTool.UI.Theming;

namespace WwTool.UI.Controls;

/// <summary>
/// 加载形象（像素风 GIF）：逐帧播放，放大时用最近邻采样保持像素颗粒。
/// 所有加载状态统一用它，不用转圈图标。减少动画时停在第一帧。
/// </summary>
/// <remarks>帧按资源地址缓存，多个控件共用同一组位图。</remarks>
public sealed class PixelGif : Control
{
    public static readonly StyledProperty<string> SourceProperty =
        AvaloniaProperty.Register<PixelGif, string>(nameof(Source), "avares://WwTool/Assets/Images/Icon.gif");

    public static readonly StyledProperty<bool> IsPlayingProperty =
        AvaloniaProperty.Register<PixelGif, bool>(nameof(IsPlaying), true);

    /// <summary>水平翻转（彩蛋里转身用）。</summary>
    public static readonly StyledProperty<bool> IsMirroredProperty =
        AvaloniaProperty.Register<PixelGif, bool>(nameof(IsMirrored));

    private static readonly Dictionary<string, Frame[]> Cache = new(StringComparer.Ordinal);

    private Frame[] _frames = [];
    private TimeSpan _start;
    private bool _startSet;
    private bool _frameRequested;
    private int _index;

    static PixelGif()
    {
        AffectsRender<PixelGif>(IsMirroredProperty);
    }

    public string Source { get => GetValue(SourceProperty); set => SetValue(SourceProperty, value); }

    public bool IsPlaying { get => GetValue(IsPlayingProperty); set => SetValue(IsPlayingProperty, value); }

    public bool IsMirrored { get => GetValue(IsMirroredProperty); set => SetValue(IsMirroredProperty, value); }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SourceProperty)
        {
            _frames = Load(Source);
            _index = 0;
            InvalidateMeasure();
            InvalidateVisual();
        }
        else if (change.Property == IsPlayingProperty) RequestFrame();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (_frames.Length == 0) _frames = Load(Source);
        _startSet = false;
        RequestFrame();
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        if (_frames.Length == 0) return default;
        PixelSize size = _frames[0].Bitmap.PixelSize;
        return new Size(size.Width, size.Height);
    }

    private void RequestFrame()
    {
        if (_frameRequested || !IsPlaying || _frames.Length < 2 || Motion.Reduced) return;
        if (TopLevel.GetTopLevel(this) is not { } top) return;
        _frameRequested = true;
        top.RequestAnimationFrame(OnFrame);
    }

    private void OnFrame(TimeSpan time)
    {
        _frameRequested = false;
        if (!_startSet) { _start = time; _startSet = true; }
        double total = _frames.Sum(f => f.Duration.TotalMilliseconds);
        double t = total <= 0 ? 0 : (time - _start).TotalMilliseconds % total;
        int index = 0;
        for (; index < _frames.Length - 1; index++)
        {
            t -= _frames[index].Duration.TotalMilliseconds;
            if (t < 0) break;
        }
        if (index != _index)
        {
            _index = index;
            InvalidateVisual();
        }
        if (TopLevel.GetTopLevel(this) is not null) RequestFrame();
    }

    public override void Render(DrawingContext context)
    {
        if (_frames.Length == 0) return;
        Bitmap bitmap = _frames[Math.Min(_index, _frames.Length - 1)].Bitmap;
        var rect = new Rect(Bounds.Size);
        using var pixelated = context.PushRenderOptions(new RenderOptions { BitmapInterpolationMode = BitmapInterpolationMode.None });
        if (IsMirrored)
        {
            using (context.PushTransform(Matrix.CreateScale(-1, 1) * Matrix.CreateTranslation(rect.Width, 0)))
            {
                context.DrawImage(bitmap, rect);
            }
            return;
        }
        context.DrawImage(bitmap, rect);
    }

    private static Frame[] Load(string uri)
    {
        lock (Cache)
        {
            if (Cache.TryGetValue(uri, out var cached)) return cached;
            try
            {
                using Stream stream = AssetLoader.Open(new Uri(uri));
                using var managed = new SKManagedStream(stream);
                using var codec = SKCodec.Create(managed);
                if (codec is null) return Cache[uri] = [];
                var info = new SKImageInfo(codec.Info.Width, codec.Info.Height, SKColorType.Bgra8888, SKAlphaType.Premul);
                SKCodecFrameInfo[] frameInfos = codec.FrameInfo;
                int count = Math.Max(1, frameInfos.Length);
                var frames = new Frame[count];
                var bitmaps = new SKBitmap[count];
                for (int i = 0; i < count; i++)
                {
                    var bmp = new SKBitmap(info);
                    int required = frameInfos.Length > 0 ? frameInfos[i].RequiredFrame : -1;
                    if (required >= 0 && bitmaps[required] is { } prior) prior.CopyTo(bmp);
                    codec.GetPixels(info, bmp.GetPixels(), new SKCodecOptions(i, required));
                    bitmaps[i] = bmp;
                    using SKImage image = SKImage.FromBitmap(bmp);
                    using SKData png = image.Encode(SKEncodedImageFormat.Png, 100);
                    using var ms = new MemoryStream(png.ToArray());
                    int duration = frameInfos.Length > 0 ? frameInfos[i].Duration : 100;
                    frames[i] = new Frame(new Bitmap(ms), TimeSpan.FromMilliseconds(duration <= 10 ? 100 : duration));
                }
                foreach (SKBitmap b in bitmaps) b.Dispose();
                return Cache[uri] = frames;
            }
            catch (Exception ex) when (ex is IOException or FileNotFoundException or InvalidOperationException)
            {
                return Cache[uri] = [];
            }
        }
    }

    private sealed record Frame(Bitmap Bitmap, TimeSpan Duration);
}
