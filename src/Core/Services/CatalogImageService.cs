using System.Collections.Concurrent;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using WwTool.Core.Abstractions;
using WwTool.Core.Abstractions.Platform;
using WwTool.Core.Infrastructure;

namespace WwTool.Core.Services;

/// <summary>
/// 资料图片服务：按需下载、解码校验、磁盘缓存及离线回退。
/// 与界面框架无关，解码交给 <see cref="IImageDecoder{TImage}"/>；WPF 与 Avalonia 各提供一个解码器。
/// </summary>
public class CatalogImageService<TImage> : ICatalogImageCache where TImage : class
{
    private const long MaxImageBytes = 20 * 1024 * 1024;
    private static readonly string[] ResourceSchemes = ["pack://application:,,,/", "avares://WwTool/"];

    private readonly IHttpClientFactory _factory;
    private readonly ILoggerService _logger;
    private readonly CatalogRepositorySource _repository;
    private readonly IImageDecoder<TImage> _decoder;
    private readonly string _appDirectory;
    private readonly string _root;
    private readonly SemaphoreSlim[] _locks = Enumerable.Range(0, 64).Select(_ => new SemaphoreSlim(1, 1)).ToArray();
    private readonly ConcurrentDictionary<(string Path, int Width), WeakReference<TImage>> _decoded = new();
    private readonly ConcurrentDictionary<string, DateTimeOffset> _failed = new();
    private readonly SemaphoreSlim _downloads = new(4, 4);
    private readonly SemaphoreSlim _commits = new(1, 1);
    private int _generation;

    public event EventHandler? Changed;
    public event EventHandler<string>? ImageReady;
    public event EventHandler? RetryRequested;

    private sealed record ImageMetadata(string RequestPath, string Url, string? ETag, DateTimeOffset? LastModified);

    public CatalogImageService(
        IHttpClientFactory factory,
        ILoggerService logger,
        CatalogRepositorySource repository,
        IImageDecoder<TImage> decoder,
        IAppPaths paths)
    {
        _factory = factory;
        _logger = logger;
        _repository = repository;
        _decoder = decoder;
        _appDirectory = paths.AppDirectory;
        _root = Path.Combine(paths.CacheDirectory, "Images");
    }

    /// <summary>页面载入时预备立绘缓存；浮层显示时只读缓存。</summary>
    /// <remarks>
    /// 只确保文件已在本地或磁盘缓存里，不解码：一页几十张立绘和武器图全部解码，
    /// 会把原生堆峰值推高几十 MB，且释放后不还给系统。
    /// 只有新下载的图片才发 <see cref="ImageReady"/>，让已打开的浮层从缓存重读。
    /// </remarks>
    public async Task PrepareHoverImagesAsync(IEnumerable<string> paths, CancellationToken token)
    {
        await Task.WhenAll(paths.Distinct().Select(async path =>
        {
            if (!await EnsureDownloadedAsync(path, token)) return;
            token.ThrowIfCancellationRequested();
            ImageReady?.Invoke(this, path);
        }));
    }

    /// <summary>本地没有、磁盘缓存也没有时下载一份；返回是否新写入了缓存。打包资源和无法解析来源的路径不处理。</summary>
    private async Task<bool> EnsureDownloadedAsync(string path, CancellationToken token)
    {
        if (!path.Contains("://", StringComparison.Ordinal) && File.Exists(Path.GetFullPath(path, _appDirectory))) return false;
        string key = Key(path);
        string cachePath = Path.Combine(_root, key + ".image");
        if (File.Exists(cachePath)) return false;
        var gate = _locks[Convert.ToByte(key[..2], 16) % _locks.Length];
        await gate.WaitAsync(token);
        try
        {
            if (File.Exists(cachePath) || IsSuppressed(key)) return false;
            int startedGeneration = _generation;
            try
            {
                string? url = await ResolveUrlAsync(path, token);
                return url is not null && await FetchAsync(path, url, key, null, token) is not null;
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !token.IsCancellationRequested)
            {
                if (_generation == startedGeneration) _failed[key] = DateTimeOffset.UtcNow;
                _logger.Debug($"浮层图片预下载失败：{ex.GetType().Name}");
                return false;
            }
        }
        finally { gate.Release(); }
    }

    /// <summary>浮层只读已有缓存与本地图，缺少立绘时回退到头像，不触发下载。</summary>
    /// <remarks>结果登记进已解码缓存，随后的 <see cref="LoadAsync"/> 直接复用，同一张图不再解码第二次。</remarks>
    public async Task<TImage?> LoadCachedAsync(string? path, string? fallbackPath, int width, CancellationToken token)
    {
        foreach (string candidate in new[] { path, fallbackPath }.OfType<string>().Where(x => !string.IsNullOrWhiteSpace(x)).Distinct())
        {
            token.ThrowIfCancellationRequested();
            if (_decoded.TryGetValue((candidate, width), out var weak) && weak.TryGetTarget(out var existing)) return existing;
            var local = DecodeLocal(candidate, width);
            if (local is not null) return Remember(candidate, width, local);
            var cached = await TryReadAsync(Path.Combine(_root, Key(candidate) + ".image"), width, token);
            if (cached is not null) return Remember(candidate, width, cached);
        }
        return DecodeLocal(_decoder.DefaultImagePath, width);
    }

    public async Task<TImage?> LoadAsync(string? path, int width, CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(path)) return DecodeLocal(_decoder.DefaultImagePath, width);
        if (_decoded.TryGetValue((path, width), out var weak) && weak.TryGetTarget(out var existing)) return existing;
        string key = Key(path);
        var gate = _locks[Convert.ToByte(key[..2], 16) % _locks.Length];
        await gate.WaitAsync(token);
        try
        {
            // 随包图片优先，远端配置中的 iconUrl 不参与图标下载。
            var local = DecodeLocal(path, width);
            if (local is not null) return Remember(path, width, local);
            string cachePath = Path.Combine(_root, key + ".image");
            // 已有有效缓存时绝不发网络请求；更新只在同步入口触发。
            var cached = await TryReadAsync(cachePath, width, token);
            if (cached is not null) return Remember(path, width, cached);
            int startedGeneration = _generation;
            if (!IsSuppressed(key))
            {
                try
                {
                    string? url = await ResolveUrlAsync(path, token);
                    if (url is not null)
                    {
                        var bytes = await FetchAsync(path, url, key, null, token);
                        if (bytes is not null && _generation == startedGeneration)
                            return Remember(path, width, _decoder.Decode(bytes, width));
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException || !token.IsCancellationRequested)
                {
                    if (_generation == startedGeneration)
                    {
                        _failed[key] = DateTimeOffset.UtcNow;
                        foreach (var entry in _failed.Where(x => DateTimeOffset.UtcNow - x.Value >= TimeSpan.FromHours(24)))
                            _failed.TryRemove(entry.Key, out _);
                    }
                    _logger.Debug($"图片下载或解码失败，使用本地回退：{ex.GetType().Name}");
                }
            }
            // 默认图不进入已解码缓存，否则失败期限结束后仍无法重试。
            return DecodeLocal(_decoder.DefaultImagePath, width);
        }
        finally { gate.Release(); }
    }

    /// <summary>只复用仍由界面持有的位图，限制键数量而不延长图片生命周期。</summary>
    private TImage? Remember(string path, int width, TImage? image)
    {
        if (image is null) return null;
        if (_decoded.Count >= 128) _decoded.Clear();
        _decoded[(path, width)] = new WeakReference<TImage>(image);
        return image;
    }

    /// <summary>失败一天内不重复请求；取消不计为失败，过期后重新尝试。</summary>
    private bool IsSuppressed(string key)
    {
        if (!_failed.TryGetValue(key, out var at)) return false;
        if (DateTimeOffset.UtcNow - at < TimeSpan.FromHours(24)) return true;
        _failed.TryRemove(key, out _);
        return false;
    }

    /// <summary>图标仅从仓库默认分支补齐；攻略站快照的立绘 URL 直接作为下载来源。</summary>
    private async Task<string?> ResolveUrlAsync(string path, CancellationToken token)
    {
        string normalized = StripResourceScheme(path.Replace('\\', '/'));
        var match = Regex.Match(normalized, @"^Local/Icons/([1-9]\d*)\.png$", RegexOptions.IgnoreCase);
        if (match.Success)
        {
            string? branch = await _repository.GetDefaultBranchAsync(false, token);
            return branch is null ? null : CatalogRepositorySource.FileUrl(branch, $"catalog/Icons/{match.Groups[1].Value}.png");
        }
        return CatalogJson.IsImageUrl(path) ? path : null;
    }

    public async Task RefreshCachedAsync(CancellationToken token, bool resetFailures = true)
    {
        _decoded.Clear();
        if (resetFailures) _failed.Clear();
        if (!Directory.Exists(_root))
        {
            Changed?.Invoke(this, EventArgs.Empty);
            if (resetFailures) RetryRequested?.Invoke(this, EventArgs.Empty);
            return;
        }
        foreach (string metadataPath in Directory.GetFiles(_root, "*.json"))
        {
            token.ThrowIfCancellationRequested();
            try
            {
                var metadata = JsonSerializer.Deserialize<ImageMetadata>(await File.ReadAllTextAsync(metadataPath, token));
                if (metadata is null) continue;
                // 本地图已存在时无需检查仓库副本；不再请求旧版本配置的外部 iconUrl。
                if (DecodeLocal(metadata.RequestPath, 32) is not null) continue;
                string key = Key(metadata.RequestPath);
                var gate = _locks[Convert.ToByte(key[..2], 16) % _locks.Length];
                await gate.WaitAsync(token);
                try
                {
                    string? url = await ResolveUrlAsync(metadata.RequestPath, token);
                    if (url is not null) await FetchAsync(metadata.RequestPath, url, key, metadata, token);
                }
                finally { gate.Release(); }
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !token.IsCancellationRequested)
            { _logger.Debug($"缓存图片更新失败，保留原图：{ex.GetType().Name}"); }
        }
        Changed?.Invoke(this, EventArgs.Empty);
        if (resetFailures) RetryRequested?.Invoke(this, EventArgs.Empty);
    }

    public async Task ClearAsync(CancellationToken token = default)
    {
        await _commits.WaitAsync(token);
        try
        {
            Interlocked.Increment(ref _generation);
            if (Directory.Exists(_root))
                foreach (string file in Directory.GetFiles(_root))
                    if (Path.GetExtension(file) is ".image" or ".json") File.Delete(file);
            _decoded.Clear();
            _failed.Clear();
        }
        finally { _commits.Release(); }
        await _repository.ResetAsync(token);
        Changed?.Invoke(this, EventArgs.Empty);
        RetryRequested?.Invoke(this, EventArgs.Empty);
    }

    private async Task<byte[]?> FetchAsync(string path, string url, string key, ImageMetadata? old, CancellationToken token)
    {
        int startedGeneration = _generation;
        await _downloads.WaitAsync(token);
        try
        {
            // 等待并发槽位只响应页面取消，下载开始后才计算网络超时。
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
            timeout.CancelAfter(TimeSpan.FromSeconds(10));
            token = timeout.Token;
            using var client = _factory.CreateClient("CatalogImageClient");
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            if (old?.Url == url && File.Exists(Path.Combine(_root, key + ".image")))
            {
                if (old.ETag is not null) request.Headers.TryAddWithoutValidation("If-None-Match", old.ETag);
                if (old.LastModified is not null) request.Headers.IfModifiedSince = old.LastModified;
            }
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);
            if (response.StatusCode == HttpStatusCode.NotModified) return null;
            response.EnsureSuccessStatusCode();
            if (response.Content.Headers.ContentLength > MaxImageBytes) throw new InvalidDataException("Image too large.");
            await using var source = await response.Content.ReadAsStreamAsync(token);
            using var output = new MemoryStream();
            byte[] buffer = new byte[81920];
            int read;
            while ((read = await source.ReadAsync(buffer, token)) > 0)
            {
                if (output.Length + read > MaxImageBytes) throw new InvalidDataException("Image too large.");
                output.Write(buffer, 0, read);
            }
            byte[] bytes = output.ToArray();
            _ = _decoder.Decode(bytes, 256); // 必须解码成功才替换旧缓存，HTML 错误页不能污染缓存。
            await _commits.WaitAsync(token);
            try
            {
                if (_generation != startedGeneration) return null;
                Directory.CreateDirectory(_root);
                string imagePath = Path.Combine(_root, key + ".image");
                await AtomicFile.WriteAsync(imagePath,
                    (stream, cancellation) => stream.WriteAsync(bytes, cancellation).AsTask(), token);
                var metadata = new ImageMetadata(path, url, response.Headers.ETag?.ToString(), response.Content.Headers.LastModified);
                await AtomicFile.WriteTextAsync(Path.Combine(_root, key + ".json"), JsonSerializer.Serialize(metadata), token);
            }
            finally { _commits.Release(); }
            return bytes;
        }
        finally { _downloads.Release(); }
    }

    private static string Key(string path) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(path)));

    private static string StripResourceScheme(string path)
    {
        foreach (string scheme in ResourceSchemes)
        {
            if (path.StartsWith(scheme, StringComparison.OrdinalIgnoreCase)) return path[scheme.Length..];
        }
        return path;
    }

    private async Task<TImage?> TryReadAsync(string path, int width, CancellationToken token)
    {
        try { return File.Exists(path) ? _decoder.Decode(await File.ReadAllBytesAsync(path, token), width) : null; }
        catch (Exception ex) when (ex is not OperationCanceledException || !token.IsCancellationRequested)
        { _logger.Debug($"缓存图片不可读：{ex.GetType().Name}"); return null; }
    }

    /// <summary>
    /// 先找程序目录下的文件（如 <c>Local/Icons/1403.png</c>），再交给解码器查找打包资源。网址一律返回 <c>null</c>。
    /// </summary>
    private TImage? DecodeLocal(string path, int width)
    {
        if (path.StartsWith("http", StringComparison.OrdinalIgnoreCase)) return null;
        try
        {
            if (!path.Contains("://", StringComparison.Ordinal))
            {
                string local = Path.GetFullPath(path, _appDirectory);
                if (File.Exists(local)) return _decoder.Decode(File.ReadAllBytes(local), width);
            }
            return _decoder.DecodeBundled(path, width);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // 各界面框架解码失败抛出的异常类型不同（WPF 可能抛 COMException），统一按“没有本地图”回退。
            _logger.Debug($"本地图片不可读：{ex.GetType().Name}");
            return null;
        }
    }
}
