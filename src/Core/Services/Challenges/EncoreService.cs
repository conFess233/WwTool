using System.Globalization;
using System.Net.Http;
using System.Text.Json;
using WwTool.Core.Abstractions;
using WwTool.Core.Abstractions.Platform;
using WwTool.Core.Composition;

namespace WwTool.Core.Services.Challenges;

/// <summary>获取结果：<see cref="Changed"/> 表示与上次缓存的内容不同（或原来没有缓存）。</summary>
public sealed record EncoreData<T>(T Value, DateTimeOffset FetchedAtUtc, bool Changed);

/// <summary>
/// encore.moe 的逆境深塔、冥歌海墟、终焉矩阵数据（接口文档 <c>docs/API/Encore.md</c>），原始 JSON 与图片缓存在 <c>Cache/Encore</c>。
/// <para>
/// 接口不支持 ETag / Last-Modified，所以不能靠条件请求判断是否最新：调用方先用缓存显示，再调用 <c>Refresh*</c> 重新获取，
/// 与缓存比较内容，有变化才替换。主接口失败（网络错误或 5xx）时改用备用接口；都失败时保留旧缓存。
/// </para>
/// </summary>
public sealed class EncoreService(IHttpClientFactory factory, IConfigService config, IAppPaths paths, ILoggerService logger)
{
    /// <summary>encore.moe 提供的语言；界面语言不在其中时用简体中文。</summary>
    public static readonly IReadOnlyList<string> Languages = ["zh-Hans", "zh-Hant", "en", "ja", "ko", "de", "es", "fr", "id", "pt", "ru", "th", "vi"];

    private const string DefaultLanguage = "zh-Hans";
    private const long MaxJsonBytes = 32L * 1024 * 1024;

    /// <summary>超过这么久没用到的详情缓存与图片会被清理。</summary>
    public static readonly TimeSpan UnusedLifetime = TimeSpan.FromDays(60);

    public TimeSpan RequestTimeout { get; init; } = TimeSpan.FromSeconds(20);

    /// <summary>测试用：当前时间。</summary>
    public Func<DateTimeOffset> Now { get; init; } = () => DateTimeOffset.UtcNow;

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    private readonly SemaphoreSlim _gate = new(1, 1);
    private RemoteImageCache? _images;
    private int _cleaned;

    public string CacheDirectory => Path.Combine(paths.CacheDirectory, "Encore");

    /// <summary>敌人头像、信物、角色头像等图片的缓存（每次显示前向服务器校验）。</summary>
    public RemoteImageCache Images => _images ??= new RemoteImageCache(factory, config, logger, Path.Combine(CacheDirectory, "Images"), "encore.moe") { Now = Now };

    public static string RequestLanguage(string uiLanguage) =>
        Languages.FirstOrDefault(l => string.Equals(l, uiLanguage, StringComparison.OrdinalIgnoreCase)) ?? DefaultLanguage;

    /// <summary>接口路径中的名字：toa / whiwa / dpmatrix。</summary>
    public static string PathName(ChallengeKind kind) => kind switch
    {
        ChallengeKind.Tower => "toa",
        ChallengeKind.Wastes => "whiwa",
        _ => "dpmatrix",
    };

    /// <summary>encore.moe 上对应的网页。</summary>
    public string PageUrl(ChallengeKind kind, string uiLanguage) =>
        string.Format(CultureInfo.InvariantCulture, config.Api.Urls.EncorePageUrl, PathName(kind), RequestLanguage(uiLanguage));

    // ── 各期列表 ──────────────────────────────────────────

    public EncoreData<IReadOnlyList<ChallengeSeason>>? LoadSeasons(ChallengeKind kind, string uiLanguage) =>
        Parse(ReadEntry(SeasonsPath(kind, RequestLanguage(uiLanguage))), false, body => EncoreParser.ParseSeasons(kind, body));

    public async Task<EncoreData<IReadOnlyList<ChallengeSeason>>?> RefreshSeasonsAsync(ChallengeKind kind, string uiLanguage, CancellationToken token = default)
    {
        string language = RequestLanguage(uiLanguage);
        (CacheEntry? entry, bool changed) = await FetchAsync($"/{language}/{PathName(kind)}", SeasonsPath(kind, language), token).ConfigureAwait(false);
        return Parse(entry, changed, body => EncoreParser.ParseSeasons(kind, body));
    }

    // ── 某一期详情 ────────────────────────────────────────

    /// <summary>缓存的详情：<see cref="TowerDetail"/>、<see cref="WastesDetail"/> 或 <see cref="MatrixDetail"/>。</summary>
    public EncoreData<object>? LoadDetail(ChallengeKind kind, int id, string uiLanguage) =>
        Parse(ReadEntry(DetailPath(kind, id, RequestLanguage(uiLanguage))), false, body => ParseDetail(kind, id, body));

    public async Task<EncoreData<object>?> RefreshDetailAsync(ChallengeKind kind, int id, string uiLanguage, CancellationToken token = default)
    {
        string language = RequestLanguage(uiLanguage);
        string path = string.Create(CultureInfo.InvariantCulture, $"/{language}/{PathName(kind)}/{id}");
        (CacheEntry? entry, bool changed) = await FetchAsync(path, DetailPath(kind, id, language), token).ConfigureAwait(false);
        if (Interlocked.Exchange(ref _cleaned, 1) == 0) _ = Task.Run(CleanUp, CancellationToken.None);
        return Parse(entry, changed, body => ParseDetail(kind, id, body));
    }

    public static object ParseDetail(ChallengeKind kind, int id, string body) => kind switch
    {
        ChallengeKind.Tower => EncoreParser.ParseTower(id, body),
        ChallengeKind.Wastes => EncoreParser.ParseWastes(id, body),
        _ => EncoreParser.ParseMatrix(id, body),
    };

    private EncoreData<T>? Parse<T>(CacheEntry? entry, bool changed, Func<string, T> parse)
    {
        if (entry?.Body is null) return null;
        try
        {
            return new EncoreData<T>(parse(entry.Body), entry.FetchedAtUtc, changed);
        }
        catch (JsonException ex)
        {
            logger.Debug($"encore.moe 数据解析失败：{ex.Message}");
            return null;
        }
    }

    // ── 请求 ──────────────────────────────────────────────

    /// <summary>依次请求主接口与备用接口；成功时写入缓存并返回是否与旧缓存不同，都失败时返回 (null, false)。</summary>
    private async Task<(CacheEntry? Entry, bool Changed)> FetchAsync(string path, string cachePath, CancellationToken token)
    {
        HttpClient client = factory.CreateClient(CoreServiceCollectionExtensions.ApiClient);
        foreach (string baseUrl in new[] { config.Api.Urls.EncoreApiUrl, config.Api.Urls.EncoreApiFallbackUrl })
        {
            if (string.IsNullOrWhiteSpace(baseUrl)) continue;
            string url = baseUrl.TrimEnd('/') + path;
            try
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(token);
                cts.CancelAfter(RequestTimeout);
                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                if (!string.IsNullOrWhiteSpace(config.Api.CommonHeaders.UserAgent))
                    request.Headers.TryAddWithoutValidation("User-Agent", config.Api.CommonHeaders.UserAgent);
                request.Headers.TryAddWithoutValidation("Accept", "application/json");
                using HttpResponseMessage response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cts.Token).ConfigureAwait(false);
                int status = (int)response.StatusCode;
                if (status is >= 400 and < 500)
                {
                    // 4xx 换备用接口也一样（期数不存在等），直接放弃。
                    logger.Warn($"encore.moe 请求失败：{status} {path}");
                    return (null, false);
                }
                response.EnsureSuccessStatusCode();
                if (response.Content.Headers.ContentLength > MaxJsonBytes) throw new InvalidDataException("数据过大");
                string body = await response.Content.ReadAsStringAsync(cts.Token).ConfigureAwait(false);
                using (JsonDocument doc = JsonDocument.Parse(body))
                {
                    if (doc.RootElement.ValueKind == JsonValueKind.Object && doc.RootElement.TryGetProperty("error", out JsonElement err) && err.ValueKind == JsonValueKind.True)
                        throw new InvalidDataException("服务器返回错误");
                }
                CacheEntry? old = ReadEntry(cachePath);
                bool changed = old?.Body != body;
                var entry = new CacheEntry { FetchedAtUtc = Now(), Body = body };
                await WriteEntryAsync(cachePath, entry, token).ConfigureAwait(false);
                return (entry, changed);
            }
            catch (Exception ex) when (IsRecoverable(ex, token))
            {
                logger.Debug($"encore.moe 请求失败（{baseUrl}）：{ex.GetType().Name} {ex.Message}");
            }
        }
        logger.Warn($"encore.moe 获取失败：{path}");
        return (null, false);
    }

    // ── 缓存文件 ──────────────────────────────────────────

    private string SeasonsPath(ChallengeKind kind, string language) =>
        Path.Combine(CacheDirectory, language, PathName(kind) + ".json");

    private string DetailPath(ChallengeKind kind, int id, string language) =>
        Path.Combine(CacheDirectory, language, string.Create(CultureInfo.InvariantCulture, $"{PathName(kind)}-{id}.json"));

    private static CacheEntry? ReadEntry(string path)
    {
        try
        {
            return File.Exists(path) ? JsonSerializer.Deserialize<CacheEntry>(File.ReadAllText(path), JsonOptions) : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return null;
        }
    }

    private async Task WriteEntryAsync(string path, CacheEntry entry, CancellationToken token)
    {
        await _gate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            await File.WriteAllTextAsync(path + ".part", JsonSerializer.Serialize(entry, JsonOptions), token).ConfigureAwait(false);
            File.Move(path + ".part", path, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.Debug($"encore.moe 缓存写入失败：{ex.GetType().Name}");
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>删除很久没有更新过的详情缓存与没有用到的图片。</summary>
    public void CleanUp()
    {
        DateTime limit = Now().UtcDateTime - UnusedLifetime;
        try
        {
            if (Directory.Exists(CacheDirectory))
            {
                foreach (string file in Directory.EnumerateFiles(CacheDirectory, "*-*.json", SearchOption.AllDirectories))
                {
                    if (File.GetLastWriteTimeUtc(file) < limit) File.Delete(file);
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.Debug($"encore.moe 缓存清理失败：{ex.GetType().Name}");
        }
        Images.CleanUpUnused(UnusedLifetime);
    }

    private static bool IsRecoverable(Exception ex, CancellationToken token) =>
        ex is HttpRequestException or JsonException or IOException or UnauthorizedAccessException or InvalidDataException or UriFormatException or FormatException
        || ex is OperationCanceledException && !token.IsCancellationRequested;

    private sealed class CacheEntry
    {
        public DateTimeOffset FetchedAtUtc { get; set; }
        public string? Body { get; set; }
    }
}
