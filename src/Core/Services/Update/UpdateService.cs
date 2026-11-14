using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using WwTool.Core.Abstractions;
using WwTool.Core.Abstractions.Platform;

namespace WwTool.Core.Services.Update;

/// <summary>发布里的一个压缩包。<see cref="Sha256"/> 取自 GitHub 的 <c>digest</c>（小写十六进制），没有时为空。</summary>
public sealed record ReleaseAsset(string Name, long Size, string? Sha256, string DownloadUrl);

/// <summary>一个正式发布。</summary>
public sealed record ReleaseInfo(string Tag, AppVersion Version, string Title, string Notes, string PageUrl, DateTimeOffset? PublishedAt, IReadOnlyList<ReleaseAsset> Assets);

public enum UpdateCheckStatus
{
    /// <summary>当前已是最新（或比最新发布还新）。</summary>
    UpToDate,

    /// <summary>有更新的正式版本。</summary>
    UpdateAvailable,

    /// <summary>当前是开发版（版本号带后缀），不提示更新。</summary>
    DevelopmentBuild,

    /// <summary>网络或接口失败，且没有可用的缓存。</summary>
    Failed,
}

public sealed record UpdateCheckResult(UpdateCheckStatus Status, ReleaseInfo? Latest, DateTimeOffset? CheckedAtUtc);

/// <summary>
/// 检查新版本：读 GitHub「最新正式发布」接口（只含正式版，不含预发布与草稿）。
/// 结果缓存在 <c>Cache/Update/latest.json</c>：自动检查 24 小时内直接用缓存；手动检查强制请求，
/// 但带上 ETag，没有变化时 GitHub 返回 304、不计入每小时 60 次的匿名配额。网络失败时退回缓存。
/// </summary>
public sealed class UpdateService(IHttpClientFactory factory, IConfigService config, IAppPaths paths, ILoggerService logger)
{
    /// <summary>更新专用的 HTTP 客户端名（带 User-Agent，GitHub 接口要求）。</summary>
    public const string ClientName = "UpdateClient";

    public static readonly TimeSpan CheckInterval = TimeSpan.FromHours(24);

    public TimeSpan RequestTimeout { get; init; } = TimeSpan.FromSeconds(15);

    /// <summary>当前时间，测试可替换。</summary>
    public Func<DateTimeOffset> UtcNow { get; init; } = () => DateTimeOffset.UtcNow;

    private readonly SemaphoreSlim _gate = new(1, 1);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
    };

    public string CacheDirectory => Path.Combine(paths.CacheDirectory, "Update");

    private string CachePath => Path.Combine(CacheDirectory, "latest.json");

    /// <summary>检查更新。<paramref name="force"/> 为 true 时不看 24 小时间隔（手动检查）。</summary>
    public async Task<UpdateCheckResult> CheckAsync(string currentVersion, bool force, CancellationToken token = default)
    {
        if (!AppVersion.TryParse(currentVersion, out AppVersion current) || current.IsPrerelease)
            return new UpdateCheckResult(UpdateCheckStatus.DevelopmentBuild, null, null);

        await _gate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            CacheEntry? cache = ReadCache();
            DateTimeOffset now = UtcNow();
            if (!force && cache is { Release: not null } && now - cache.CheckedAtUtc < CheckInterval)
                return Result(current, ToRelease(cache.Release), cache.CheckedAtUtc);

            ReleaseDto? fetched;
            string? etag;
            try
            {
                (fetched, etag) = await FetchAsync(cache?.ETag, token).ConfigureAwait(false);
            }
            catch (Exception ex) when (IsRecoverable(ex, token))
            {
                logger.Warn($"检查更新失败：{ex.GetType().Name} {ex.Message}");
                // 上次已知有新版本时沿用（仍然值得提示）；否则不能凭旧结果说「已是最新」，报告失败。
                UpdateCheckResult? stale = cache?.Release is { } cached ? Result(current, ToRelease(cached), cache.CheckedAtUtc) : null;
                return stale is { Status: UpdateCheckStatus.UpdateAvailable }
                    ? stale
                    : new UpdateCheckResult(UpdateCheckStatus.Failed, stale?.Latest, cache?.CheckedAtUtc);
            }

            // 304：沿用缓存的发布，只刷新检查时间。
            ReleaseDto? release = fetched ?? cache?.Release;
            if (release is null) return new UpdateCheckResult(UpdateCheckStatus.Failed, null, null);
            WriteCache(new CacheEntry { CheckedAtUtc = now, ETag = fetched is null ? cache?.ETag : etag, Release = release });
            return Result(current, ToRelease(release), now);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>上次检查的缓存结果（不联网）；没有缓存或当前是开发版时返回 null。</summary>
    public UpdateCheckResult? LoadCached(string currentVersion)
    {
        if (!AppVersion.TryParse(currentVersion, out AppVersion current) || current.IsPrerelease) return null;
        CacheEntry? cache = ReadCache();
        return cache?.Release is { } release ? Result(current, ToRelease(release), cache.CheckedAtUtc) : null;
    }

    private static UpdateCheckResult Result(AppVersion current, ReleaseInfo? latest, DateTimeOffset? checkedAt) =>
        latest is not null && latest.Version > current
            ? new UpdateCheckResult(UpdateCheckStatus.UpdateAvailable, latest, checkedAt)
            : new UpdateCheckResult(UpdateCheckStatus.UpToDate, latest, checkedAt);

    private async Task<(ReleaseDto? Release, string? ETag)> FetchAsync(string? etag, CancellationToken token)
    {
        string url = config.Api.Urls.LatestReleaseUrl;
        if (string.IsNullOrWhiteSpace(url)) throw new InvalidOperationException("未配置更新地址");
        HttpClient client = factory.CreateClient(ClientName);
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(token);
        cts.CancelAfter(RequestTimeout);
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Accept.ParseAdd("application/vnd.github+json");
        if (!string.IsNullOrEmpty(etag) && EntityTagHeaderValue.TryParse(etag, out EntityTagHeaderValue? tag)) request.Headers.IfNoneMatch.Add(tag);
        using HttpResponseMessage response = await client.SendAsync(request, cts.Token).ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.NotModified) return (null, etag);
        response.EnsureSuccessStatusCode();
        string json = await response.Content.ReadAsStringAsync(cts.Token).ConfigureAwait(false);
        ReleaseDto dto = JsonSerializer.Deserialize<ReleaseDto>(json, JsonOptions) ?? throw new JsonException("空的发布数据");
        if (dto.Draft || dto.Prerelease) throw new InvalidDataException("最新发布不是正式版");
        if (!AppVersion.TryParse(dto.TagName, out _)) throw new InvalidDataException($"无法识别的版本号：{dto.TagName}");
        return (dto, response.Headers.ETag?.ToString());
    }

    private static ReleaseInfo? ToRelease(ReleaseDto dto)
    {
        if (!AppVersion.TryParse(dto.TagName, out AppVersion version)) return null;
        var assets = (dto.Assets ?? [])
            .Where(a => !string.IsNullOrWhiteSpace(a.Name) && Uri.TryCreate(a.BrowserDownloadUrl, UriKind.Absolute, out Uri? u) && IsAllowedDownload(u))
            .Select(a => new ReleaseAsset(a.Name!, a.Size, ParseDigest(a.Digest), a.BrowserDownloadUrl!))
            .ToList();
        string page = Uri.TryCreate(dto.HtmlUrl, UriKind.Absolute, out Uri? p) && p.Scheme == Uri.UriSchemeHttps ? dto.HtmlUrl! : string.Empty;
        return new ReleaseInfo(dto.TagName!, version, string.IsNullOrWhiteSpace(dto.Name) ? dto.TagName! : dto.Name!,
            (dto.Body ?? string.Empty).Replace("\r\n", "\n"), page, dto.PublishedAt, assets);
    }

    /// <summary>只接受 https 下载地址；调试版另外允许本机 http（本地验证更新流程用的假发布服务）。</summary>
    private static bool IsAllowedDownload(Uri url)
    {
#if DEBUG
        if (url.Scheme == Uri.UriSchemeHttp && url.IsLoopback) return true;
#endif
        return url.Scheme == Uri.UriSchemeHttps;
    }

    /// <summary>GitHub 的 <c>digest</c> 形如 <c>sha256:abcd…</c>；只认 64 位十六进制。</summary>
    internal static string? ParseDigest(string? digest)
    {
        if (string.IsNullOrWhiteSpace(digest) || !digest.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase)) return null;
        string hex = digest[7..].Trim().ToLowerInvariant();
        return hex.Length == 64 && hex.All(Uri.IsHexDigit) ? hex : null;
    }

    private CacheEntry? ReadCache()
    {
        try
        {
            return File.Exists(CachePath) ? JsonSerializer.Deserialize<CacheEntry>(File.ReadAllText(CachePath), JsonOptions) : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return null;
        }
    }

    private void WriteCache(CacheEntry entry)
    {
        try
        {
            Directory.CreateDirectory(CacheDirectory);
            File.WriteAllText(CachePath + ".part", JsonSerializer.Serialize(entry, JsonOptions));
            File.Move(CachePath + ".part", CachePath, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.Debug($"更新检查缓存写入失败：{ex.GetType().Name}");
        }
    }

    internal static bool IsRecoverable(Exception ex, CancellationToken token) =>
        ex is HttpRequestException or JsonException or IOException or InvalidDataException or InvalidOperationException or UriFormatException
        || ex is OperationCanceledException && !token.IsCancellationRequested;

    private sealed class CacheEntry
    {
        public DateTimeOffset CheckedAtUtc { get; set; }
        public string? ETag { get; set; }
        public ReleaseDto? Release { get; set; }
    }

    internal sealed class ReleaseDto
    {
        [JsonPropertyName("tag_name")] public string? TagName { get; set; }
        public string? Name { get; set; }
        public string? Body { get; set; }
        [JsonPropertyName("html_url")] public string? HtmlUrl { get; set; }
        public bool Draft { get; set; }
        public bool Prerelease { get; set; }
        [JsonPropertyName("published_at")] public DateTimeOffset? PublishedAt { get; set; }
        public List<AssetDto>? Assets { get; set; }
    }

    internal sealed class AssetDto
    {
        public string? Name { get; set; }
        public long Size { get; set; }
        public string? Digest { get; set; }
        [JsonPropertyName("browser_download_url")] public string? BrowserDownloadUrl { get; set; }
    }
}
