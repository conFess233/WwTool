using System.Globalization;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using WwTool.Core.Abstractions;
using WwTool.Core.Abstractions.Platform;

namespace WwTool.Core.Services;

/// <summary>启动器资讯所属区服。</summary>
public enum LauncherRegion
{
    /// <summary>国际服（按界面语言获取）。</summary>
    Global,

    /// <summary>国服（只有简体中文）。</summary>
    China,
}

/// <summary>资讯栏目：活动 / 公告 / 新闻。</summary>
public enum LauncherNewsCategory
{
    Activity,
    Notice,
    News,
}

/// <summary>一条资讯。<see cref="Date"/> 由接口的「MM-dd」推断年份得到。</summary>
public sealed record LauncherNewsItem(LauncherNewsCategory Category, string Title, Uri? Link, DateTime? Date, string DateText);

/// <summary>一张轮播图：本地缓存的图片与点击跳转地址。</summary>
public sealed record LauncherNewsSlide(string ImagePath, Uri? Link);

/// <summary>一次获取的资讯。</summary>
public sealed record LauncherNews(
    LauncherRegion Region,
    string Language,
    IReadOnlyList<LauncherNewsItem> Items,
    IReadOnlyList<LauncherNewsSlide> Slides,
    DateTimeOffset FetchedAtUtc);

/// <summary>
/// 游戏启动器首页资讯（接口文档 <c>docs/API/Launcher.md</c>「启动器资讯」）：国服 / 国际服分别获取，
/// 原始 JSON 与轮播图缓存在 <c>Cache/LauncherNews</c>，离线时显示上次的结果。
/// </summary>
public sealed class LauncherNewsService(IHttpClientFactory factory, IConfigService config, IAppPaths paths, ILoggerService logger)
{
    private const long MaxImageBytes = 16L * 1024 * 1024;

    /// <summary>单次请求超时；失败后再试一次（国服域名偶尔首次连接卡住，紧接着重试即可成功）。</summary>
    public TimeSpan RequestTimeout { get; init; } = TimeSpan.FromSeconds(8);

    public int Attempts { get; init; } = 2;
    public TimeSpan ImageTimeout { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>测试用：「今天」，用于推断没有年份的日期。</summary>
    public Func<DateTime> Today { get; init; } = () => DateTime.Today;

    private readonly SemaphoreSlim _gate = new(1, 1);

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public string CacheDirectory => Path.Combine(paths.CacheDirectory, "LauncherNews");

    /// <summary>国服只有简体中文；国际服按界面语言。</summary>
    public static string RequestLanguage(LauncherRegion region, string uiLanguage) =>
        region == LauncherRegion.China || string.IsNullOrWhiteSpace(uiLanguage) ? "zh-Hans" : uiLanguage;

    /// <summary>上次缓存的资讯；没有或损坏时返回 null。</summary>
    public LauncherNews? LoadCached(LauncherRegion region, string uiLanguage)
    {
        string language = RequestLanguage(region, uiLanguage);
        try
        {
            string path = CachePath(region, language);
            if (!File.Exists(path)) return null;
            CacheEntry? entry = JsonSerializer.Deserialize<CacheEntry>(File.ReadAllText(path), JsonOptions);
            if (entry?.Response is null) return null;
            return Build(region, language, entry.Response, entry.FetchedAtUtc, onlyExistingImages: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// 从接口获取文字内容并缓存，立即返回（只带已缓存的轮播图）；轮播图另由 <see cref="DownloadSlidesAsync"/> 下载。
    /// 失败返回 null（保留旧缓存）。
    /// </summary>
    public async Task<LauncherNews?> RefreshAsync(LauncherRegion region, string uiLanguage, CancellationToken token = default)
    {
        string language = RequestLanguage(region, uiLanguage);
        string template = region == LauncherRegion.China ? config.Api.Urls.LauncherNewsUrlCN : config.Api.Urls.LauncherNewsUrlNET;
        if (string.IsNullOrWhiteSpace(template)) return null;
        string url = string.Format(CultureInfo.InvariantCulture, template, Uri.EscapeDataString(language));

        InformationDto? response = null;
        HttpClient client = factory.CreateClient("WwToolClient");
        for (int attempt = 1; response is null; attempt++)
        {
            try
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(token);
                cts.CancelAfter(RequestTimeout);
                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                AddHeaders(request);
                using HttpResponseMessage http = await client.SendAsync(request, cts.Token).ConfigureAwait(false);
                http.EnsureSuccessStatusCode();
                string json = await http.Content.ReadAsStringAsync(cts.Token).ConfigureAwait(false);
                response = JsonSerializer.Deserialize<InformationDto>(json, JsonOptions) ?? throw new JsonException("空的资讯数据");
            }
            catch (Exception ex) when (IsRecoverable(ex, token))
            {
                // 404 与数据格式错误重试无用。
                bool retryable = ex is not JsonException && !(ex is HttpRequestException { StatusCode: { } code } && (int)code is >= 400 and < 500);
                if (attempt >= Attempts || !retryable)
                {
                    logger.Warn($"启动器资讯（{region}）获取失败：{ex.GetType().Name} {ex.Message}");
                    return null;
                }
                logger.Debug($"启动器资讯（{region}）第 {attempt} 次请求失败，重试：{ex.GetType().Name}");
            }
        }

        var entry = new CacheEntry { FetchedAtUtc = DateTimeOffset.UtcNow, Response = response };
        await _gate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            Directory.CreateDirectory(CacheDirectory);
            string path = CachePath(region, language);
            File.WriteAllText(path + ".part", JsonSerializer.Serialize(entry, JsonOptions));
            File.Move(path + ".part", path, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.Debug($"启动器资讯缓存写入失败：{ex.GetType().Name}");
        }
        finally
        {
            _gate.Release();
        }
        return Build(region, language, response, entry.FetchedAtUtc, onlyExistingImages: true);
    }

    /// <summary>
    /// 下载缓存资讯里还没有的轮播图（最多 3 张并行，单张失败跳过），再清理不再引用的旧图。
    /// 有新图下载成功时返回带上它们的资讯，否则返回 null。
    /// </summary>
    public async Task<LauncherNews?> DownloadSlidesAsync(LauncherRegion region, string uiLanguage, CancellationToken token = default)
    {
        string language = RequestLanguage(region, uiLanguage);
        CacheEntry? entry;
        try
        {
            string path = CachePath(region, language);
            if (!File.Exists(path)) return null;
            entry = JsonSerializer.Deserialize<CacheEntry>(File.ReadAllText(path), JsonOptions);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return null;
        }
        if (entry?.Response is null) return null;

        HttpClient client = factory.CreateClient("WwToolClient");
        var missing = (entry.Response.Slideshow ?? [])
            .Select(slide => (slide, name: ImageFileName(slide)))
            .Where(x => x.name is not null && !File.Exists(Path.Combine(CacheDirectory, x.name)))
            .DistinctBy(x => x.name)
            .ToList();
        int downloaded = 0;
        await Parallel.ForEachAsync(missing, new ParallelOptions { MaxDegreeOfParallelism = 3, CancellationToken = token }, async (x, ct) =>
        {
            try
            {
                await DownloadAsync(client, x.slide.Url!, Path.Combine(CacheDirectory, x.name!), ct).ConfigureAwait(false);
                Interlocked.Increment(ref downloaded);
            }
            catch (Exception ex) when (IsRecoverable(ex, ct))
            {
                logger.Debug($"启动器资讯轮播图下载失败：{ex.GetType().Name}");
            }
        }).ConfigureAwait(false);

        await _gate.WaitAsync(token).ConfigureAwait(false);
        try { CleanUp(); }
        finally { _gate.Release(); }
        return downloaded > 0 ? Build(region, language, entry.Response, entry.FetchedAtUtc, onlyExistingImages: true) : null;
    }

    private LauncherNews Build(LauncherRegion region, string language, InformationDto response, DateTimeOffset fetchedAt, bool onlyExistingImages)
    {
        DateTime today = Today();
        var items = new List<LauncherNewsItem>();
        void Add(SectionDto? section, LauncherNewsCategory category)
        {
            if (section is not { FunctionSwitch: 1 } || section.Contents is null) return;
            foreach (ContentDto content in section.Contents)
            {
                if (string.IsNullOrWhiteSpace(content.Content)) continue;
                DateTime? date = InferDate(content.Time, today);
                items.Add(new LauncherNewsItem(category, CollapseSpaces(content.Content), ToUri(content.JumpUrl), date,
                    date?.ToString("MM/dd", CultureInfo.InvariantCulture) ?? content.Time?.Trim() ?? string.Empty));
            }
        }
        Add(response.Guidance?.Activity, LauncherNewsCategory.Activity);
        Add(response.Guidance?.Notice, LauncherNewsCategory.Notice);
        Add(response.Guidance?.News, LauncherNewsCategory.News);

        var slides = new List<LauncherNewsSlide>();
        foreach (SlideDto slide in response.Slideshow ?? [])
        {
            if (ImageFileName(slide) is not { } name) continue;
            string file = Path.Combine(CacheDirectory, name);
            if (onlyExistingImages && !File.Exists(file)) continue;
            slides.Add(new LauncherNewsSlide(file, ToUri(slide.JumpUrl)));
        }
        return new LauncherNews(region, language, items, slides, fetchedAt);
    }

    /// <summary>
    /// 接口日期只有「MM-dd」：按今年解析，若比今天晚 7 天以上则视为去年（跨年时的旧公告）。
    /// </summary>
    public static DateTime? InferDate(string? text, DateTime today)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        string[] parts = text.Trim().Split('-', '/', '.');
        if (parts.Length != 2 || !int.TryParse(parts[0], out int month) || !int.TryParse(parts[1], out int day)) return null;
        if (month is < 1 or > 12 || day < 1) return null;
        int year = today.Year;
        if (day > DateTime.DaysInMonth(year, month)) return null;
        var date = new DateTime(year, month, day);
        if (date > today.AddDays(7))
        {
            year--;
            if (day > DateTime.DaysInMonth(year, month)) return null;
            date = new DateTime(year, month, day);
        }
        return date;
    }

    private static string CollapseSpaces(string text) => string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    /// <summary>轮播图按内容 MD5 命名（同一张图国服 / 国际服地址不同但可共用）；没有 MD5 时用地址末段。</summary>
    private static string? ImageFileName(SlideDto slide)
    {
        if (!Uri.TryCreate(slide.Url, UriKind.Absolute, out Uri? uri) || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp)) return null;
        string ext = Path.GetExtension(uri.AbsolutePath).ToLowerInvariant();
        if (ext is not (".jpg" or ".jpeg" or ".png" or ".webp")) ext = ".img";
        string key = slide.Md5 is { Length: 32 } md5 && md5.All(Uri.IsHexDigit)
            ? md5.ToLowerInvariant()
            : Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(slide.Url!)), 0, 16).ToLowerInvariant();
        return "slide-" + key + ext;
    }

    private static Uri? ToUri(string? url) =>
        Uri.TryCreate(url, UriKind.Absolute, out Uri? uri) && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp) ? uri : null;

    private string CachePath(LauncherRegion region, string language) =>
        Path.Combine(CacheDirectory, $"{(region == LauncherRegion.China ? "cn" : "global")}-{language}.json");

    private async Task DownloadAsync(HttpClient client, string url, string target, CancellationToken token)
    {
        string temp = target + ".part";
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(token);
        cts.CancelAfter(ImageTimeout);
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            AddHeaders(request);
            using HttpResponseMessage response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cts.Token).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            if (response.Content.Headers.ContentLength > MaxImageBytes) throw new InvalidDataException("轮播图过大");
            byte[] data = await response.Content.ReadAsByteArrayAsync(cts.Token).ConfigureAwait(false);
            if (data.Length == 0 || data.Length > MaxImageBytes) throw new InvalidDataException("轮播图大小异常");
            await File.WriteAllBytesAsync(temp, data, cts.Token).ConfigureAwait(false);
            File.Move(temp, target, overwrite: true);
        }
        finally
        {
            try { if (File.Exists(temp)) File.Delete(temp); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
    }

    /// <summary>删除所有缓存 JSON 都不再引用的轮播图。</summary>
    private void CleanUp()
    {
        try
        {
            var keep = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string json in Directory.EnumerateFiles(CacheDirectory, "*.json"))
            {
                CacheEntry? entry = JsonSerializer.Deserialize<CacheEntry>(File.ReadAllText(json), JsonOptions);
                foreach (SlideDto slide in entry?.Response?.Slideshow ?? [])
                {
                    if (ImageFileName(slide) is { } name) keep.Add(name);
                }
            }
            foreach (string file in Directory.EnumerateFiles(CacheDirectory, "slide-*"))
            {
                if (!keep.Contains(Path.GetFileName(file))) File.Delete(file);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            logger.Debug($"启动器资讯缓存清理失败：{ex.GetType().Name}");
        }
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
        public InformationDto? Response { get; set; }
    }

    private sealed class InformationDto
    {
        public GuidanceDto? Guidance { get; set; }
        public List<SlideDto>? Slideshow { get; set; }
    }

    private sealed class GuidanceDto
    {
        public SectionDto? Activity { get; set; }
        public SectionDto? Notice { get; set; }
        public SectionDto? News { get; set; }
    }

    private sealed class SectionDto
    {
        public string? Title { get; set; }
        public int Sort { get; set; }
        public int FunctionSwitch { get; set; }
        public List<ContentDto>? Contents { get; set; }
    }

    private sealed class ContentDto
    {
        public string? Content { get; set; }
        public string? JumpUrl { get; set; }
        public string? Time { get; set; }
    }

    private sealed class SlideDto
    {
        public string? Url { get; set; }
        public string? JumpUrl { get; set; }
        public string? Md5 { get; set; }

        [JsonPropertyName("carouselNotes")]
        public string? CarouselNotes { get; set; }
    }
}
