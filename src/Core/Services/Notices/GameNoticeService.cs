using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using WwTool.Core.Abstractions;
using WwTool.Core.Abstractions.Platform;
using WwTool.Core.Composition;

namespace WwTool.Core.Services.Notices;

/// <summary>
/// 游戏内公告（接口文档 <c>docs/API/GameNotice.md</c>）：按区服与语言获取，原始 JSON 与图片缓存在 <c>Cache/GameNotice</c>。
/// <para>
/// 每次刷新都带 ETag / Last-Modified 做条件请求，服务器返回 304 才继续用缓存；图片（版本日历图、横幅、正文配图）同样按需校验，
/// 不会只凭缓存存在就认为是最新的。离线时显示上次的结果。
/// </para>
/// </summary>
public sealed class GameNoticeService(IHttpClientFactory factory, IConfigService config, IAppPaths paths, ILoggerService logger)
{
    /// <summary>接口提供的语言；界面语言不在其中时用简体中文。</summary>
    public static readonly IReadOnlyList<string> Languages = ["zh-Hans", "zh-Hant", "en", "ja", "ko", "de", "es", "fr", "th"];

    private const string ChineseLanguage = "zh-Hans";
    private const long MaxJsonBytes = 16L * 1024 * 1024;
    private const long MaxImageBytes = 16L * 1024 * 1024;

    public TimeSpan RequestTimeout { get; init; } = TimeSpan.FromSeconds(10);
    public int Attempts { get; init; } = 2;
    public TimeSpan ImageTimeout { get; init; } = TimeSpan.FromSeconds(40);

    /// <summary>同一张图在本次运行中校验过后，这么久内不再重复校验。</summary>
    public TimeSpan ImageRevalidateInterval { get; init; } = TimeSpan.FromMinutes(30);

    /// <summary>测试用：当前时间。</summary>
    public Func<DateTimeOffset> Now { get; init; } = () => DateTimeOffset.UtcNow;

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _imageLocks = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, DateTimeOffset> _imageValidated = new(StringComparer.Ordinal);

    public string CacheDirectory => Path.Combine(paths.CacheDirectory, "GameNotice");
    public string ImageDirectory => Path.Combine(CacheDirectory, "Images");

    public static string RequestLanguage(string uiLanguage) =>
        Languages.FirstOrDefault(l => string.Equals(l, uiLanguage, StringComparison.OrdinalIgnoreCase)) ?? ChineseLanguage;

    /// <summary>上次缓存的公告；没有或损坏时返回 null。</summary>
    public GameNoticeFeed? LoadCached(LauncherRegion region, string uiLanguage)
    {
        string language = RequestLanguage(uiLanguage);
        CacheEntry? shown = ReadEntry(region, language);
        if (shown?.Body is null) return null;
        string? chinese = language == ChineseLanguage ? null : ReadEntry(region, ChineseLanguage)?.Body;
        try
        {
            return NoticeParser.Parse(region, language, shown.Body, chinese, shown.FetchedAtUtc);
        }
        catch (JsonException ex)
        {
            logger.Debug($"游戏公告缓存损坏：{ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// 从接口获取（条件请求）展示语言版本；不是简体中文时同时获取简体中文版用于解析时间与分类。
    /// 展示语言获取失败时返回 null（保留旧缓存）；只有中文版失败时用它的旧缓存。
    /// </summary>
    public async Task<GameNoticeFeed?> RefreshAsync(LauncherRegion region, string uiLanguage, CancellationToken token = default)
    {
        string language = RequestLanguage(uiLanguage);
        Task<CacheEntry?> shownTask = FetchAsync(region, language, token);
        Task<CacheEntry?> chineseTask = language == ChineseLanguage ? Task.FromResult<CacheEntry?>(null) : FetchAsync(region, ChineseLanguage, token);
        CacheEntry? shown = await shownTask.ConfigureAwait(false);
        CacheEntry? chinese = await chineseTask.ConfigureAwait(false);
        if (shown?.Body is null) return null;
        string? chineseBody = language == ChineseLanguage ? null : (chinese ?? ReadEntry(region, ChineseLanguage))?.Body;
        GameNoticeFeed feed;
        try
        {
            feed = NoticeParser.Parse(region, language, shown.Body, chineseBody, shown.FetchedAtUtc);
        }
        catch (JsonException ex)
        {
            logger.Warn($"游戏公告（{region}）解析失败：{ex.Message}");
            return null;
        }
        _ = Task.Run(CleanUpImages, CancellationToken.None);
        return feed;
    }

    private string Url(LauncherRegion region, string language)
    {
        string template = region == LauncherRegion.China ? config.Api.Urls.GameNoticeUrlCN : config.Api.Urls.GameNoticeUrlNET;
        return string.IsNullOrWhiteSpace(template) ? string.Empty : string.Format(CultureInfo.InvariantCulture, template, Uri.EscapeDataString(language));
    }

    /// <summary>条件请求一份公告 JSON：200 写入缓存，304 只更新获取时间；失败返回 null。</summary>
    private async Task<CacheEntry?> FetchAsync(LauncherRegion region, string language, CancellationToken token)
    {
        string url = Url(region, language);
        if (url.Length == 0) return null;
        CacheEntry? cached = ReadEntry(region, language);
        HttpClient client = factory.CreateClient(CoreServiceCollectionExtensions.ApiClient);
        for (int attempt = 1; ; attempt++)
        {
            try
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(token);
                cts.CancelAfter(RequestTimeout);
                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                AddHeaders(request);
                if (cached?.Body is not null) AddValidators(request, cached.ETag, cached.LastModified);
                using HttpResponseMessage response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cts.Token).ConfigureAwait(false);
                CacheEntry entry;
                if (response.StatusCode == HttpStatusCode.NotModified && cached?.Body is not null)
                {
                    entry = new CacheEntry { FetchedAtUtc = Now(), ETag = cached.ETag, LastModified = cached.LastModified, Body = cached.Body };
                }
                else
                {
                    response.EnsureSuccessStatusCode();
                    if (response.Content.Headers.ContentLength > MaxJsonBytes) throw new InvalidDataException("公告数据过大");
                    string body = await response.Content.ReadAsStringAsync(cts.Token).ConfigureAwait(false);
                    using (JsonDocument.Parse(body)) { }
                    entry = new CacheEntry
                    {
                        FetchedAtUtc = Now(),
                        ETag = response.Headers.ETag?.ToString(),
                        LastModified = response.Content.Headers.LastModified,
                        Body = body,
                    };
                }
                await WriteEntryAsync(region, language, entry, token).ConfigureAwait(false);
                return entry;
            }
            catch (Exception ex) when (IsRecoverable(ex, token))
            {
                bool retryable = ex is not JsonException && !(ex is HttpRequestException { StatusCode: { } code } && (int)code is >= 400 and < 500);
                if (attempt >= Attempts || !retryable)
                {
                    logger.Warn($"游戏公告（{region} {language}）获取失败：{ex.GetType().Name} {ex.Message}");
                    return null;
                }
                logger.Debug($"游戏公告（{region} {language}）第 {attempt} 次请求失败，重试：{ex.GetType().Name}");
            }
        }
    }

    // ── 图片 ──────────────────────────────────────────────

    /// <summary>已缓存的图片路径（不校验）；没有时返回 null。</summary>
    public string? CachedImage(string? url)
    {
        if (ImagePath(url) is not { } path) return null;
        return File.Exists(path) ? path : null;
    }

    /// <summary>
    /// 取图片：没有缓存时下载；有缓存时向服务器做条件请求校验（本次运行在 <see cref="ImageRevalidateInterval"/> 内校验过的跳过，
    /// <paramref name="force"/> 为 true 时总是校验），图片变了就替换。
    /// 返回本地路径（网络失败时为旧缓存，没有缓存时为 null）与内容是否有变化。
    /// </summary>
    public async Task<(string? Path, bool Changed)> GetImageAsync(string? url, bool force = false, CancellationToken token = default)
    {
        if (ImagePath(url) is not { } path) return (null, false);
        SemaphoreSlim gate = _imageLocks.GetOrAdd(path, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            bool exists = File.Exists(path);
            if (exists && !force && _imageValidated.TryGetValue(url!, out DateTimeOffset at) && Now() - at < ImageRevalidateInterval)
                return (path, false);

            ImageMeta? meta = exists ? ReadMeta(path) : null;
            HttpClient client = factory.CreateClient(CoreServiceCollectionExtensions.ApiClient);
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(token);
            cts.CancelAfter(ImageTimeout);
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                AddHeaders(request);
                request.Headers.Accept.Clear();
                request.Headers.TryAddWithoutValidation("Accept", "image/avif,image/webp,image/png,image/jpeg,*/*");
                if (exists) AddValidators(request, meta?.ETag, meta?.LastModified);
                using HttpResponseMessage response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cts.Token).ConfigureAwait(false);
                if (response.StatusCode == HttpStatusCode.NotModified && exists)
                {
                    _imageValidated[url!] = Now();
                    return (path, false);
                }
                response.EnsureSuccessStatusCode();
                if (response.Content.Headers.ContentLength > MaxImageBytes) throw new InvalidDataException("图片过大");
                byte[] data = await response.Content.ReadAsByteArrayAsync(cts.Token).ConfigureAwait(false);
                if (data.Length == 0 || data.Length > MaxImageBytes) throw new InvalidDataException("图片大小异常");
                bool changed = !exists || !SameContent(path, data);
                Directory.CreateDirectory(ImageDirectory);
                string temp = path + ".part";
                await File.WriteAllBytesAsync(temp, data, cts.Token).ConfigureAwait(false);
                File.Move(temp, path, overwrite: true);
                WriteMeta(path, new ImageMeta
                {
                    Url = url,
                    ETag = response.Headers.ETag?.ToString(),
                    LastModified = response.Content.Headers.LastModified,
                });
                _imageValidated[url!] = Now();
                return (path, changed);
            }
            catch (Exception ex) when (IsRecoverable(ex, token))
            {
                logger.Debug($"公告图片获取失败：{ex.GetType().Name} {ex.Message}");
                TryDelete(path + ".part");
                return (exists ? path : null, false);
            }
        }
        finally
        {
            gate.Release();
        }
    }

    private string? ImagePath(string? url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out Uri? uri) || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp)) return null;
        string ext = Path.GetExtension(uri.AbsolutePath).ToLowerInvariant();
        if (ext is not (".jpg" or ".jpeg" or ".png" or ".webp" or ".gif")) ext = ".img";
        string key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(uri.AbsoluteUri)), 0, 16).ToLowerInvariant();
        return Path.Combine(ImageDirectory, key + ext);
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

    /// <summary>删除所有缓存公告都不再引用的图片（横幅、日历图、正文配图）。</summary>
    public void CleanUpImages()
    {
        try
        {
            if (!Directory.Exists(ImageDirectory)) return;
            var keep = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string json in Directory.EnumerateFiles(CacheDirectory, "*.json"))
            {
                CacheEntry? entry = JsonSerializer.Deserialize<CacheEntry>(File.ReadAllText(json), JsonOptions);
                if (entry?.Body is null) continue;
                foreach (string url in ReferencedImages(entry.Body))
                {
                    if (ImagePath(url) is { } path) keep.Add(Path.GetFileNameWithoutExtension(path));
                }
            }
            foreach (string file in Directory.EnumerateFiles(ImageDirectory))
            {
                string name = Path.GetFileName(file);
                int dot = name.IndexOf('.');
                string key = dot > 0 ? name[..dot] : name;
                if (!keep.Contains(key)) File.Delete(file);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            logger.Debug($"游戏公告图片清理失败：{ex.GetType().Name}");
        }
    }

    private static IEnumerable<string> ReferencedImages(string body)
    {
        using JsonDocument doc = JsonDocument.Parse(body);
        var urls = new List<string>();
        foreach (JsonProperty section in doc.RootElement.EnumerateObject())
        {
            if (section.Value.ValueKind != JsonValueKind.Array) continue;
            foreach (JsonElement item in section.Value.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object) continue;
                foreach (string name in new[] { "tabBanner", "foldBanner", "backgroundUrl" })
                {
                    if (item.TryGetProperty(name, out JsonElement v) && v.ValueKind == JsonValueKind.String && v.GetString() is { } s) urls.Add(s);
                }
                if (item.TryGetProperty("content", out JsonElement c) && c.ValueKind == JsonValueKind.String)
                    urls.AddRange(NoticeHtml.ImageUrls(NoticeHtml.Parse(c.GetString())));
            }
        }
        return urls;
    }

    // ── 缓存文件 ──────────────────────────────────────────

    private string CachePath(LauncherRegion region, string language) =>
        Path.Combine(CacheDirectory, $"{(region == LauncherRegion.China ? "cn" : "global")}-{language}.json");

    private CacheEntry? ReadEntry(LauncherRegion region, string language)
    {
        try
        {
            string path = CachePath(region, language);
            return File.Exists(path) ? JsonSerializer.Deserialize<CacheEntry>(File.ReadAllText(path), JsonOptions) : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return null;
        }
    }

    private async Task WriteEntryAsync(LauncherRegion region, string language, CacheEntry entry, CancellationToken token)
    {
        await _gate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            Directory.CreateDirectory(CacheDirectory);
            string path = CachePath(region, language);
            await File.WriteAllTextAsync(path + ".part", JsonSerializer.Serialize(entry, JsonOptions), token).ConfigureAwait(false);
            File.Move(path + ".part", path, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.Debug($"游戏公告缓存写入失败：{ex.GetType().Name}");
        }
        finally
        {
            _gate.Release();
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
            logger.Debug($"公告图片校验信息写入失败：{ex.GetType().Name}");
        }
    }

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    private static void AddValidators(HttpRequestMessage request, string? etag, DateTimeOffset? lastModified)
    {
        if (!string.IsNullOrWhiteSpace(etag) && EntityTagHeaderValue.TryParse(etag, out EntityTagHeaderValue? tag))
            request.Headers.IfNoneMatch.Add(tag);
        if (lastModified is { } modified) request.Headers.IfModifiedSince = modified;
    }

    private void AddHeaders(HttpRequestMessage request)
    {
        if (!string.IsNullOrWhiteSpace(config.Api.CommonHeaders.UserAgent))
            request.Headers.TryAddWithoutValidation("User-Agent", config.Api.CommonHeaders.UserAgent);
        request.Headers.TryAddWithoutValidation("Accept", "application/json, text/plain, */*");
    }

    private static bool IsRecoverable(Exception ex, CancellationToken token) =>
        ex is HttpRequestException or JsonException or IOException or UnauthorizedAccessException or InvalidDataException or UriFormatException or FormatException
        || ex is OperationCanceledException && !token.IsCancellationRequested;

    private sealed class CacheEntry
    {
        public DateTimeOffset FetchedAtUtc { get; set; }
        public string? ETag { get; set; }
        public DateTimeOffset? LastModified { get; set; }
        public string? Body { get; set; }
    }

    private sealed class ImageMeta
    {
        public string? Url { get; set; }
        public string? ETag { get; set; }
        public DateTimeOffset? LastModified { get; set; }
    }
}
