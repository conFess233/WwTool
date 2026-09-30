using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows.Media.Imaging;
using WwTool.Common.Utils;
using WwTool.Services.Interfaces;

namespace WwTool.Services;

/// <summary>WPF 图片适配服务：按需下载、解码校验、磁盘缓存及离线回退。</summary>
public sealed class CatalogImageService(IHttpClientFactory factory, ILoggerService logger, CatalogRepositorySource repository)
{
    private const string DefaultImage = "pack://application:,,,/UI/Resources/Images/Default.png";
    private readonly string root = Path.Combine(AppContext.BaseDirectory, "Local", "Cache", "Images");
    private readonly SemaphoreSlim[] locks = Enumerable.Range(0, 64).Select(_ => new SemaphoreSlim(1, 1)).ToArray();
    private readonly ConcurrentDictionary<(string Path, int Width), WeakReference<BitmapSource>> decoded = new();
    private readonly ConcurrentDictionary<string, DateTimeOffset> failed = new();
    private readonly SemaphoreSlim downloads = new(4, 4);
    private readonly SemaphoreSlim commits = new(1, 1);
    private int generation;
    public event EventHandler? Changed;
    public event EventHandler<string>? ImageReady;
    public event EventHandler? RetryRequested;

    private sealed record ImageMetadata(string RequestPath, string Url, string? ETag, DateTimeOffset? LastModified);

    /// <summary>页面载入时预备立绘缓存；浮层显示时只读缓存。</summary>
    public async Task PrepareHoverImagesAsync(IEnumerable<string> paths, CancellationToken token)
    {
        await Task.WhenAll(paths.Distinct().Select(async path =>
        {
            await LoadAsync(path, 360, token);
            token.ThrowIfCancellationRequested();
            ImageReady?.Invoke(this, path);
        }));
    }

    /// <summary>浮层只读已有缓存与本地图，缺少立绘时回退到头像，不触发下载。</summary>
    public async Task<BitmapSource?> LoadCachedAsync(string? path, string? fallbackPath, int width, CancellationToken token)
    {
        foreach (string candidate in new[] { path, fallbackPath }.OfType<string>().Where(x => !string.IsNullOrWhiteSpace(x)).Distinct())
        {
            token.ThrowIfCancellationRequested();
            var local = DecodeLocal(candidate, width);
            if (local is not null) return local;
            var cached = await TryReadAsync(Path.Combine(root, Key(candidate) + ".image"), width, token);
            if (cached is not null) return cached;
        }
        return DecodeLocal(DefaultImage, width);
    }

    public async Task<BitmapSource?> LoadAsync(string? path, int width, CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(path)) return DecodeLocal(DefaultImage, width);
        if (decoded.TryGetValue((path, width), out var weak) && weak.TryGetTarget(out var existing)) return existing;
        string key = Key(path);
        var gate = locks[Convert.ToByte(key[..2], 16) % locks.Length];
        await gate.WaitAsync(token);
        try
        {
            // 随包图片优先，远端配置中的 iconUrl 不参与图标下载。
            var local = DecodeLocal(path, width);
            if (local is not null) return Remember(path, width, local);
            string cachePath = Path.Combine(root, key + ".image");
            // 已有有效缓存时绝不发网络请求；更新只在同步入口触发。
            var cached = await TryReadAsync(cachePath, width, token);
            if (cached is not null) return Remember(path, width, cached);
            int startedGeneration = generation;
            if (!IsSuppressed(key))
            {
                try
                {
                    string? url = await ResolveUrlAsync(path, token);
                    if (url is not null)
                    {
                        var bytes = await FetchAsync(path, url, key, null, token);
                        if (bytes is not null && generation == startedGeneration)
                            return Remember(path, width, Decode(bytes, width));
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException || !token.IsCancellationRequested)
                {
                    if (generation == startedGeneration)
                    {
                        failed[key] = DateTimeOffset.UtcNow;
                        foreach (var entry in failed.Where(x => DateTimeOffset.UtcNow - x.Value >= TimeSpan.FromHours(24)))
                            failed.TryRemove(entry.Key, out _);
                    }
                    logger.Debug($"图片下载或解码失败，使用本地回退：{ex.GetType().Name}");
                }
            }
            // 默认图不进入已解码缓存，否则失败期限结束后仍无法重试。
            return DecodeLocal(DefaultImage, width);
        }
        finally { gate.Release(); }
    }

    /// <summary>只复用仍由界面持有的冻结位图，限制键数量而不延长图片生命周期。</summary>
    private BitmapSource? Remember(string path, int width, BitmapSource? image)
    {
        if (image is null) return null;
        if (decoded.Count >= 128) decoded.Clear();
        decoded[(path, width)] = new WeakReference<BitmapSource>(image);
        return image;
    }
    /// <summary>失败一天内不重复请求；取消不计为失败，过期后重新尝试。</summary>
    private bool IsSuppressed(string key)
    {
        if (!failed.TryGetValue(key, out var at)) return false;
        if (DateTimeOffset.UtcNow - at < TimeSpan.FromHours(24)) return true;
        failed.TryRemove(key, out _);
        return false;
    }

    /// <summary>图标仅从仓库默认分支补齐；攻略站快照的立绘 URL 直接作为下载来源。</summary>
    private async Task<string?> ResolveUrlAsync(string path, CancellationToken token)
    {
        string normalized = path.Replace('\\', '/');
        const string packPrefix = "pack://application:,,,/";
        if (normalized.StartsWith(packPrefix, StringComparison.OrdinalIgnoreCase)) normalized = normalized[packPrefix.Length..];
        var match = Regex.Match(normalized, @"^Local/Icons/([1-9]\d*)\.png$", RegexOptions.IgnoreCase);
        if (match.Success)
        {
            string? branch = await repository.GetDefaultBranchAsync(false, token);
            return branch is null ? null : CatalogRepositorySource.FileUrl(branch, $"WwTool/Local/Icons/{match.Groups[1].Value}.png");
        }
        return CatalogJson.IsImageUrl(path) ? path : null;
    }

    public async Task RefreshCachedAsync(CancellationToken token, bool resetFailures = true)
    {
        decoded.Clear();
        if (resetFailures) failed.Clear();
        if (!Directory.Exists(root))
        {
            Changed?.Invoke(this, EventArgs.Empty);
            if (resetFailures) RetryRequested?.Invoke(this, EventArgs.Empty);
            return;
        }
        foreach (string metadataPath in Directory.GetFiles(root, "*.json"))
        {
            token.ThrowIfCancellationRequested();
            try
            {
                var metadata = JsonSerializer.Deserialize<ImageMetadata>(await File.ReadAllTextAsync(metadataPath, token));
                if (metadata is null) continue;
                // 本地图已存在时无需检查仓库副本；不再请求旧版本配置的外部 iconUrl。
                if (DecodeLocal(metadata.RequestPath, 32) is not null) continue;
                string key = Key(metadata.RequestPath);
                var gate = locks[Convert.ToByte(key[..2], 16) % locks.Length];
                await gate.WaitAsync(token);
                try
                {
                    string? url = await ResolveUrlAsync(metadata.RequestPath, token);
                    if (url is not null) await FetchAsync(metadata.RequestPath, url, key, metadata, token);
                }
                finally { gate.Release(); }
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !token.IsCancellationRequested)
            { logger.Debug($"缓存图片更新失败，保留原图：{ex.GetType().Name}"); }
        }
        Changed?.Invoke(this, EventArgs.Empty);
        if (resetFailures) RetryRequested?.Invoke(this, EventArgs.Empty);
    }

    public async Task ClearAsync(CancellationToken token = default)
    {
        await commits.WaitAsync(token);
        try
        {
            Interlocked.Increment(ref generation);
            if (Directory.Exists(root))
                foreach (string file in Directory.GetFiles(root))
                    if (Path.GetExtension(file) is ".image" or ".json") File.Delete(file);
            decoded.Clear();
            failed.Clear();
        }
        finally { commits.Release(); }
        await repository.ResetAsync(token);
        Changed?.Invoke(this, EventArgs.Empty);
        RetryRequested?.Invoke(this, EventArgs.Empty);
    }

    private async Task<byte[]?> FetchAsync(string path, string url, string key, ImageMetadata? old, CancellationToken token)
    {
        int startedGeneration = generation;
        await downloads.WaitAsync(token);
        try
        {
            // 等待并发槽位只响应页面取消，下载开始后才计算网络超时。
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
            timeout.CancelAfter(TimeSpan.FromSeconds(10));
            token = timeout.Token;
            using var client = factory.CreateClient("CatalogImageClient");
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            if (old?.Url == url && File.Exists(Path.Combine(root, key + ".image")))
            {
                if (old.ETag is not null) request.Headers.TryAddWithoutValidation("If-None-Match", old.ETag);
                if (old.LastModified is not null) request.Headers.IfModifiedSince = old.LastModified;
            }
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);
            if (response.StatusCode == HttpStatusCode.NotModified) return null;
            response.EnsureSuccessStatusCode();
            if (response.Content.Headers.ContentLength > 20 * 1024 * 1024) throw new InvalidDataException("Image too large.");
            await using var source = await response.Content.ReadAsStreamAsync(token);
            using var output = new MemoryStream();
            byte[] buffer = new byte[81920];
            int read;
            while ((read = await source.ReadAsync(buffer, token)) > 0)
            {
                if (output.Length + read > 20 * 1024 * 1024) throw new InvalidDataException("Image too large.");
                output.Write(buffer, 0, read);
            }
            byte[] bytes = output.ToArray();
            _ = Decode(bytes, 256); // 必须解码成功才替换旧缓存，HTML 错误页不能污染缓存。
            await commits.WaitAsync(token);
            try
            {
                if (generation != startedGeneration) return null;
                Directory.CreateDirectory(root);
                string imagePath = Path.Combine(root, key + ".image");
                await AtomicFile.WriteAsync(imagePath,
                    (stream, cancellation) => stream.WriteAsync(bytes, cancellation).AsTask(), token);
                var metadata = new ImageMetadata(path, url, response.Headers.ETag?.ToString(), response.Content.Headers.LastModified);
                await AtomicFile.WriteTextAsync(Path.Combine(root, key + ".json"), JsonSerializer.Serialize(metadata), token);
            }
            finally { commits.Release(); }
            return bytes;
        }
        finally { downloads.Release(); }
    }

    private static string Key(string path) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(path)));

    private static BitmapSource Decode(byte[] bytes, int width)
    {
        using var stream = new MemoryStream(bytes);
        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.DecodePixelWidth = Math.Clamp(width, 16, 2048);
        image.StreamSource = stream;
        image.EndInit();
        image.Freeze();
        return image;
    }

    private async Task<BitmapSource?> TryReadAsync(string path, int width, CancellationToken token)
    {
        try { return File.Exists(path) ? Decode(await File.ReadAllBytesAsync(path, token), width) : null; }
        catch (Exception ex) when (ex is not OperationCanceledException || !token.IsCancellationRequested)
        { logger.Debug($"缓存图片不可读：{ex.GetType().Name}"); return null; }
    }

    private BitmapSource? DecodeLocal(string path, int width)
    {
        if (path.StartsWith("http", StringComparison.OrdinalIgnoreCase)) return null;
        try
        {
            if (!path.StartsWith("pack://", StringComparison.OrdinalIgnoreCase))
            {
                string local = Path.GetFullPath(path, AppContext.BaseDirectory);
                if (File.Exists(local)) return Decode(File.ReadAllBytes(local), width);
                path = "pack://application:,,,/" + path.Replace('\\', '/');
            }
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.DecodePixelWidth = Math.Clamp(width, 16, 2048);
            image.UriSource = new Uri(path, UriKind.Absolute);
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch (Exception ex) { logger.Debug($"本地图片不可读：{ex.GetType().Name}"); return null; }
    }
}
