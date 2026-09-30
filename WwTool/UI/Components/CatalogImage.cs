using System.Windows;
using System.Windows.Controls;
using WwTool.Services;

namespace WwTool.UI.Components;

/// <summary>异步图片控件，支持虚拟化复用、卸载取消及同步后刷新。</summary>
public sealed class CatalogImage : Image
{
    public static readonly DependencyProperty PathProperty = DependencyProperty.Register(nameof(Path), typeof(string), typeof(CatalogImage), new PropertyMetadata(null, OnInputChanged));
    public static readonly DependencyProperty LoaderProperty = DependencyProperty.Register(nameof(Loader), typeof(CatalogImageService), typeof(CatalogImage), new PropertyMetadata(null, OnLoaderChanged));
    public static readonly DependencyProperty DecodeWidthProperty = DependencyProperty.Register(nameof(DecodeWidth), typeof(int), typeof(CatalogImage), new PropertyMetadata(128, OnInputChanged));
    public static readonly DependencyProperty CacheOnlyProperty = DependencyProperty.Register(nameof(CacheOnly), typeof(bool), typeof(CatalogImage), new PropertyMetadata(false, OnInputChanged));
    public static readonly DependencyProperty FallbackPathProperty = DependencyProperty.Register(nameof(FallbackPath), typeof(string), typeof(CatalogImage), new PropertyMetadata(null, OnInputChanged));
    public bool CacheOnly { get => (bool)GetValue(CacheOnlyProperty); set => SetValue(CacheOnlyProperty, value); }
    public string? FallbackPath { get => (string?)GetValue(FallbackPathProperty); set => SetValue(FallbackPathProperty, value); }
    public string? Path { get => (string?)GetValue(PathProperty); set => SetValue(PathProperty, value); }
    public CatalogImageService? Loader { get => (CatalogImageService?)GetValue(LoaderProperty); set => SetValue(LoaderProperty, value); }
    public int DecodeWidth { get => (int)GetValue(DecodeWidthProperty); set => SetValue(DecodeWidthProperty, value); }
    private CancellationTokenSource? pending;

    public CatalogImage()
    {
        Loaded += (_, _) => { Subscribe(); Refresh(); };
        Unloaded += (_, _) => { Unsubscribe(); pending?.Cancel(); };
    }

    private static void OnInputChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e) => ((CatalogImage)sender).Refresh();
    private static void OnLoaderChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
    {
        var image = (CatalogImage)sender;
        if (e.OldValue is CatalogImageService old)
        {
            old.Changed -= image.OnImagesChanged;
            old.ImageReady -= image.OnImageReady;
        }
        image.Subscribe();
        image.Refresh();
    }
    private void Subscribe()
    {
        if (!IsLoaded || Loader is null) return;
        Loader.Changed -= OnImagesChanged;
        Loader.Changed += OnImagesChanged;
        Loader.ImageReady -= OnImageReady;
        Loader.ImageReady += OnImageReady;
    }
    private void Unsubscribe()
    {
        if (Loader is null) return;
        Loader.Changed -= OnImagesChanged;
        Loader.ImageReady -= OnImageReady;
    }
    private void OnImagesChanged(object? sender, EventArgs e) => Dispatcher.InvokeAsync(Refresh);
    /// <summary>单张预载完成只刷新使用该图片的控件，避免取消其他图片的下载。</summary>
    private void OnImageReady(object? sender, string path) => Dispatcher.InvokeAsync(() =>
    {
        if (Path == path || FallbackPath == path) Refresh();
    });

    private async void Refresh()
    {
        if (!IsLoaded || Loader is null) return;
        pending?.Cancel();
        pending?.Dispose();
        pending = new CancellationTokenSource();
        var token = pending.Token;
        var loader = Loader;
        string? path = Path;
        int width = DecodeWidth;
        bool cacheOnly = CacheOnly;
        string? fallback = FallbackPath;
        try
        {
            // 先展示本地图、缓存或回退图，下载期间不留空白。
            var initial = await Task.Run(() => loader.LoadCachedAsync(path, fallback, width, token), token);
            await Dispatcher.InvokeAsync(() =>
            {
                if (!token.IsCancellationRequested && IsLoaded) Source = initial;
            });
            if (cacheOnly) return;
            var source = await Task.Run(() => loader.LoadAsync(path, width, token), token);
            await Dispatcher.InvokeAsync(() =>
            {
                if (!token.IsCancellationRequested && IsLoaded) Source = source;
            });
        }
        catch (OperationCanceledException) { /* 虚拟化复用或控件卸载，忽略过期结果。 */ }
    }
}
