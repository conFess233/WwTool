using System.Collections.Concurrent;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using WwTool.Core.Abstractions;
using WwTool.Core.Composition;

namespace WwTool.Core.Services;

/// <summary>
/// 网络图片的本地缓存：一个 URL 对应目录下一个文件（文件名为 URL 的哈希），旁边的 <c>.meta</c> 记下 ETag / Last-Modified。
/// 每次取图都向服务器做条件请求（本次运行 <see cref="RevalidateInterval"/> 内校验过的跳过），图片变了就替换；离线时用旧文件。
/// 游戏公告与 encore.moe 各用一个目录，互不清理对方的图。
/// </summary>
public sealed class RemoteImageCache(IHttpClientFactory factory, IConfigService config, ILoggerService logger, string directory, string label)
{
    private const long MaxImageBytes = 16L * 1024 * 1024;

    private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, DateTimeOffset> _validated = new(StringComparer.Ordinal);

    public string Directory { get; } = directory;

    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(40);

    /// <summary>同一张图在本次运行中校验过后，这么久内不再重复校验。</summary>
    public TimeSpan RevalidateInterval { get; init; } = TimeSpan.FromMinutes(30);

    /// <summary>测试用：当前时间。</summary>
    public Func<DateTimeOffset> Now { get; init; } = () => DateTimeOffset.UtcNow;

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    /// <summary>已缓存的图片路径（不校验）；没有时返回 null。</summary>
    public string? Cached(string? url)
    {
        if (PathOf(url) is not { } path) return null;
        return File.Exists(path) ? path : null;
    }

    /// <summary>
    /// 取图片：没有缓存时下载；有缓存时向服务器做条件请求校验（<paramref name="force"/> 为 true 时总是校验），图片变了就替换。
    /// 返回本地路径（网络失败时为旧缓存，没有缓存时为 null）与内容是否有变化。
    /// </summary>
    public async Task<(string? Path, bool Changed)> GetAsync(string? url, bool force = false, CancellationToken token = default)
    {
        if (PathOf(url) is not { } path) return (null, false);
        SemaphoreSlim gate = _locks.GetOrAdd(path, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            bool exists = File.Exists(path);
            if (exists && !force && _validated.TryGetValue(url!, out DateTimeOffset at) && Now() - at < RevalidateInterval)
                return (path, false);

            ImageMeta? meta = exists ? ReadMeta(path) : null;
            HttpClient client = factory.CreateClient(CoreServiceCollectionExtensions.ApiClient);
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(token);
            cts.CancelAfter(Timeout);
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                if (!string.IsNullOrWhiteSpace(config.Api.CommonHeaders.UserAgent))
                    request.Headers.TryAddWithoutValidation("User-Agent", config.Api.CommonHeaders.UserAgent);
                request.Headers.TryAddWithoutValidation("Accept", "image/avif,image/webp,image/png,image/jpeg,*/*");
                if (exists) AddValidators(request, meta?.ETag, meta?.LastModified);
                using HttpResponseMessage response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cts.Token).ConfigureAwait(false);
                if (response.StatusCode == HttpStatusCode.NotModified && exists)
                {
                    _validated[url!] = Now();
                    Touch(path + ".meta");
                    return (path, false);
                }
                response.EnsureSuccessStatusCode();
                if (response.Content.Headers.ContentLength > MaxImageBytes) throw new InvalidDataException("图片过大");
                byte[] data = await response.Content.ReadAsByteArrayAsync(cts.Token).ConfigureAwait(false);
                if (data.Length == 0 || data.Length > MaxImageBytes) throw new InvalidDataException("图片大小异常");
                bool changed = !exists || !SameContent(path, data);
                System.IO.Directory.CreateDirectory(Directory);
                string temp = path + ".part";
                await File.WriteAllBytesAsync(temp, data, cts.Token).ConfigureAwait(false);
                File.Move(temp, path, overwrite: true);
                WriteMeta(path, new ImageMeta
                {
                    Url = url,
                    ETag = response.Headers.ETag?.ToString(),
                    LastModified = response.Content.Headers.LastModified,
                });
                _validated[url!] = Now();
                return (path, changed);
            }
            catch (Exception ex) when (IsRecoverable(ex, token))
            {
                logger.Debug($"{label}图片获取失败：{ex.GetType().Name} {ex.Message}");
                TryDelete(path + ".part");
                return (exists ? path : null, false);
            }
        }
        finally
        {
            gate.Release();
        }
    }

    /// <summary>缓存文件的路径（不论是否存在）；不是 http(s) 地址时返回 null。</summary>
    public string? PathOf(string? url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out Uri? uri) || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp)) return null;
        string ext = Path.GetExtension(uri.AbsolutePath).ToLowerInvariant();
        if (ext is not (".jpg" or ".jpeg" or ".png" or ".webp" or ".gif")) ext = ".img";
        string key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(uri.AbsoluteUri)), 0, 16).ToLowerInvariant();
        return Path.Combine(Directory, key + ext);
    }

    /// <summary>删除 <paramref name="keep"/> 返回 false 的图片（参数为文件名中的哈希）。</summary>
    public void CleanUp(Func<string, bool> keep)
    {
        try
        {
            if (!System.IO.Directory.Exists(Directory)) return;
            foreach (string file in System.IO.Directory.EnumerateFiles(Directory))
            {
                if (!keep(KeyOf(file))) File.Delete(file);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.Debug($"{label}图片清理失败：{ex.GetType().Name}");
        }
    }

    /// <summary>删除超过 <paramref name="age"/> 没有用到（下载或校验）的图片。</summary>
    public void CleanUpUnused(TimeSpan age)
    {
        DateTime limit = Now().UtcDateTime - age;
        var recent = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            if (!System.IO.Directory.Exists(Directory)) return;
            foreach (string file in System.IO.Directory.EnumerateFiles(Directory))
            {
                if (File.GetLastWriteTimeUtc(file) >= limit) recent.Add(KeyOf(file));
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return;
        }
        CleanUp(recent.Contains);
    }

    /// <summary>URL 对应的文件名哈希。</summary>
    public string? KeyOfUrl(string? url) => PathOf(url) is { } path ? KeyOf(path) : null;

    private static string KeyOf(string file)
    {
        string name = Path.GetFileName(file);
        int dot = name.IndexOf('.');
        return dot > 0 ? name[..dot] : name;
    }

    private static bool SameContent(string path, byte[] data)
    {
        try
        {
            var info = new FileInfo(path);
            return info.Exists && info.Length == data.Length && File.ReadAllBytes(path).AsSpan().SequenceEqual(data);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static ImageMeta? ReadMeta(string imagePath)
    {
        try
        {
            string path = imagePath + ".meta";
            return File.Exists(path) ? JsonSerializer.Deserialize<ImageMeta>(File.ReadAllText(path), JsonOptions) : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return null;
        }
    }

    private void WriteMeta(string imagePath, ImageMeta meta)
    {
        try
        {
            File.WriteAllText(imagePath + ".meta", JsonSerializer.Serialize(meta, JsonOptions));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.Debug($"{label}图片校验信息写入失败：{ex.GetType().Name}");
        }
    }

    private void Touch(string path)
    {
        try { if (File.Exists(path)) File.SetLastWriteTimeUtc(path, Now().UtcDateTime); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    internal static void AddValidators(HttpRequestMessage request, string? etag, DateTimeOffset? lastModified)
    {
        if (!string.IsNullOrWhiteSpace(etag) && EntityTagHeaderValue.TryParse(etag, out EntityTagHeaderValue? tag))
            request.Headers.IfNoneMatch.Add(tag);
        if (lastModified is { } modified) request.Headers.IfModifiedSince = modified;
    }

    private static bool IsRecoverable(Exception ex, CancellationToken token) =>
        ex is HttpRequestException or IOException or UnauthorizedAccessException or InvalidDataException or UriFormatException or FormatException
        || ex is OperationCanceledException && !token.IsCancellationRequested;

    private sealed class ImageMeta
    {
        public string? Url { get; set; }
        public string? ETag { get; set; }
        public DateTimeOffset? LastModified { get; set; }
    }
}
