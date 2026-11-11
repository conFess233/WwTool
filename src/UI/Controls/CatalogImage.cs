using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using WwTool.UI.Services;

namespace WwTool.UI.Controls;

/// <summary>
/// 异步资料图片：先显示本地图或缓存，再按需下载；卸载或复用时取消过期请求，缓存刷新后重新加载。
/// 加载器由组合根通过 <see cref="Loader"/> 设置一次。
/// </summary>
public sealed class CatalogImage : Image
{
    public static readonly StyledProperty<string?> PathProperty =
        AvaloniaProperty.Register<CatalogImage, string?>(nameof(Path));

    public static readonly StyledProperty<string?> FallbackPathProperty =
        AvaloniaProperty.Register<CatalogImage, string?>(nameof(FallbackPath));

    public static readonly StyledProperty<int> DecodeWidthProperty =
        AvaloniaProperty.Register<CatalogImage, int>(nameof(DecodeWidth), 128);

    public static readonly StyledProperty<bool> CacheOnlyProperty =
        AvaloniaProperty.Register<CatalogImage, bool>(nameof(CacheOnly));

    /// <summary>全局图片加载器；未设置时控件保持空白。</summary>
    public static AvaloniaCatalogImageService? Loader { get; set; }

    private CancellationTokenSource? _pending;
    private AvaloniaCatalogImageService? _subscribed;

    protected override Type StyleKeyOverride => typeof(Image);

    public string? Path
    {
        get => GetValue(PathProperty);
        set => SetValue(PathProperty, value);
    }

    public string? FallbackPath
    {
        get => GetValue(FallbackPathProperty);
        set => SetValue(FallbackPathProperty, value);
    }

    public int DecodeWidth
    {
        get => GetValue(DecodeWidthProperty);
        set => SetValue(DecodeWidthProperty, value);
    }

    /// <summary>只读本地图与缓存，不触发下载。</summary>
    public bool CacheOnly
    {
        get => GetValue(CacheOnlyProperty);
        set => SetValue(CacheOnlyProperty, value);
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        _subscribed = Loader;
        if (_subscribed is not null)
        {
            _subscribed.Changed += OnImagesChanged;
            _subscribed.RetryRequested += OnImagesChanged;
            _subscribed.ImageReady += OnImageReady;
        }
        Refresh();
    }

    protected override void OnUnloaded(RoutedEventArgs e)
    {
        base.OnUnloaded(e);
        if (_subscribed is not null)
        {
            _subscribed.Changed -= OnImagesChanged;
            _subscribed.RetryRequested -= OnImagesChanged;
            _subscribed.ImageReady -= OnImageReady;
            _subscribed = null;
        }
        _pending?.Cancel();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == PathProperty || change.Property == FallbackPathProperty
            || change.Property == DecodeWidthProperty || change.Property == CacheOnlyProperty)
        {
            Refresh();
        }
    }

    private void OnImagesChanged(object? sender, EventArgs e) => Dispatcher.UIThread.Post(Refresh);

    // 单张预载完成只刷新使用该图片的控件，避免取消其他图片的下载。
    private void OnImageReady(object? sender, string path) => Dispatcher.UIThread.Post(() =>
    {
        if (Path == path || FallbackPath == path) Refresh();
    });

    private async void Refresh()
    {
        AvaloniaCatalogImageService? loader = Loader;
        if (!IsLoaded || loader is null) return;
        _pending?.Cancel();
        _pending?.Dispose();
        var cts = new CancellationTokenSource();
        _pending = cts;
        CancellationToken token = cts.Token;
        string? path = Path;
        string? fallback = FallbackPath;
        int width = DecodeWidth;
        bool cacheOnly = CacheOnly;
        try
        {
            // 先展示本地图、缓存或回退图，下载期间不留空白。
            Bitmap? initial = await Task.Run(() => loader.LoadCachedAsync(path, fallback, width, token), token);
            if (token.IsCancellationRequested) return;
            Source = initial;
            if (cacheOnly) return;
            Bitmap? loaded = await Task.Run(() => loader.LoadAsync(path, width, token), token);
            if (!token.IsCancellationRequested) Source = loaded;
        }
        catch (OperationCanceledException)
        {
            // 控件复用或卸载，忽略过期结果。
        }
    }
}
