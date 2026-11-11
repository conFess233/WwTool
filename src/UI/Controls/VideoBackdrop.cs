using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using WwTool.UI.Services.Video;

namespace WwTool.UI.Controls;

/// <summary>
/// 动态背景：循环播放 <see cref="Source"/> 视频（Windows Media Foundation，其他平台不显示），
/// 按 UniformToFill 铺满。解码尺寸 = 刚好盖住控件的物理像素尺寸（不超过视频原尺寸），控件大小变化后约 250ms 跟随；
/// <see cref="IsPlaying"/> 为 false 时暂停并保留当前画面。第一帧出来前不绘制，下面的静态首帧图照常显示。
/// </summary>
public sealed class VideoBackdrop : Control
{
    public static readonly StyledProperty<string?> SourceProperty =
        AvaloniaProperty.Register<VideoBackdrop, string?>(nameof(Source));

    public static readonly StyledProperty<bool> IsPlayingProperty =
        AvaloniaProperty.Register<VideoBackdrop, bool>(nameof(IsPlaying));

    public static readonly DirectProperty<VideoBackdrop, bool> HasFrameProperty =
        AvaloniaProperty.RegisterDirect<VideoBackdrop, bool>(nameof(HasFrame), o => o.HasFrame);

    public string? Source { get => GetValue(SourceProperty); set => SetValue(SourceProperty, value); }

    /// <summary>解码失败（已停止播放，回到静态首帧图）。</summary>
    public event EventHandler<Exception>? PlaybackFailed;
    public bool IsPlaying { get => GetValue(IsPlayingProperty); set => SetValue(IsPlayingProperty, value); }

    /// <summary>已经有画面可显示。</summary>
    public bool HasFrame
    {
        get => _hasFrame;
        private set => SetAndRaise(HasFrameProperty, ref _hasFrame, value);
    }

    private bool _hasFrame;

    /// <summary>当前显示的帧与正在写入的帧（双缓冲），由 <see cref="_sync"/> 保护。</summary>
    private WriteableBitmap? _front, _back;
    private readonly object _sync = new();
    private bool _invalidatePending;

    private MfVideoLoop? _loop;
    private readonly DispatcherTimer _resizeTimer;

    static VideoBackdrop()
    {
        IsHitTestVisibleProperty.OverrideDefaultValue<VideoBackdrop>(false);
        ClipToBoundsProperty.OverrideDefaultValue<VideoBackdrop>(true);
    }

    public VideoBackdrop()
    {
        _resizeTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        _resizeTimer.Tick += (_, _) =>
        {
            _resizeTimer.Stop();
            if (IsWindows && _loop is not null && TargetSize() is var (w, h)) _loop.SetTargetSize(w, h);
        };
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SourceProperty)
        {
            Restart();
        }
        else if (change.Property == IsPlayingProperty)
        {
            if (IsPlaying && _loop is null) Restart();
            else if (!IsWindows) return;
            else if (IsPlaying) _loop?.Play();
            else _loop?.Pause();
        }
        else if (change.Property == BoundsProperty && _loop is not null)
        {
            _resizeTimer.Stop();
            _resizeTimer.Start();
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Restart();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        Stop();
    }

    [System.Runtime.Versioning.SupportedOSPlatformGuard("windows")]
    private static bool IsWindows => OperatingSystem.IsWindows();

    /// <summary>控件的物理像素尺寸；解码器按它算出盖住它所需、保持宽高比的输出尺寸。</summary>
    private (int Width, int Height)? TargetSize()
    {
        double scaling = TopLevel.GetTopLevel(this)?.RenderScaling ?? 1;
        double w = Bounds.Width * scaling, h = Bounds.Height * scaling;
        if (w < 2 || h < 2) return null;
        return ((int)Math.Ceiling(w), (int)Math.Ceiling(h));
    }

    private void Restart()
    {
        Stop();
        if (!IsWindows || !IsPlaying || string.IsNullOrEmpty(Source) || !File.Exists(Source)
            || TopLevel.GetTopLevel(this) is null) return;
        (int w, int h) = TargetSize() ?? (1920, 1080);
        _loop = new MfVideoLoop(Source, w, h, OnFrame, OnError);
        _loop.Play();
    }

    private void Stop()
    {
        _resizeTimer.Stop();
        if (_loop is not null && IsWindows) _loop.Dispose();
        _loop = null;
        lock (_sync)
        {
            Retire(_front);
            Retire(_back);
            _front = _back = null;
        }
        HasFrame = false;
        InvalidateVisual();
    }

    /// <summary>
    /// 不再使用的位图延后释放：渲染线程可能还在画上一帧，立即释放会让它读到已释放的位图。
    /// </summary>
    private static void Retire(WriteableBitmap? bitmap)
    {
        if (bitmap is null) return;
        DispatcherTimer.RunOnce(bitmap.Dispose, TimeSpan.FromSeconds(1));
    }

    private void OnError(Exception ex)
    {
        Dispatcher.UIThread.Post(() =>
        {
            // 解码失败：停止并回到静态首帧图。
            Stop();
            PlaybackFailed?.Invoke(this, ex);
        });
    }

    /// <summary>解码线程回调：写进后台缓冲后交换，再请求重绘（合并多次请求）。</summary>
    private void OnFrame(IntPtr scan0, int pitch, int width, int height)
    {
        lock (_sync)
        {
            if (_back is null || _back.PixelSize.Width != width || _back.PixelSize.Height != height)
            {
                WriteableBitmap? old = _back;
                if (old is not null) Dispatcher.UIThread.Post(() => Retire(old));
                _back = new WriteableBitmap(new PixelSize(width, height), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Opaque);
            }
            using (ILockedFramebuffer fb = _back.Lock())
            {
                int rowBytes = width * 4;
                unsafe
                {
                    byte* src = (byte*)scan0;
                    byte* dst = (byte*)fb.Address;
                    for (int y = 0; y < height; y++)
                    {
                        Buffer.MemoryCopy(src + (long)y * pitch, dst + (long)y * fb.RowBytes, fb.RowBytes, rowBytes);
                    }
                }
            }
            (_front, _back) = (_back, _front);
            if (_invalidatePending) return;
            _invalidatePending = true;
        }
        Dispatcher.UIThread.Post(() =>
        {
            lock (_sync) _invalidatePending = false;
            HasFrame = _front is not null;
            InvalidateVisual();
        }, DispatcherPriority.Render);
    }

    public override void Render(DrawingContext context)
    {
        WriteableBitmap? frame;
        lock (_sync) frame = _front;
        if (frame is null) return;

        // UniformToFill：按较大的缩放比铺满，居中裁切。
        Size size = Bounds.Size;
        double fw = frame.Size.Width, fh = frame.Size.Height;
        if (fw <= 0 || fh <= 0 || size.Width <= 0 || size.Height <= 0) return;
        double scale = Math.Max(size.Width / fw, size.Height / fh);
        double sw = size.Width / scale, sh = size.Height / scale;
        var source = new Rect((fw - sw) / 2, (fh - sh) / 2, sw, sh);
        lock (_sync)
        {
            if (!ReferenceEquals(frame, _front)) return;
            context.DrawImage(frame, source, new Rect(size));
        }
    }
}
