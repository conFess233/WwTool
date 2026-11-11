using System.Globalization;
using System.Net.Http;
using System.Text.Json;
using WwTool.Core.Abstractions;
using WwTool.Core.Abstractions.Platform;

namespace WwTool.Core.Services;

/// <summary>已下载到本地的启动器背景：背景图（视频背景取首帧图）与可选的标语图（与背景同尺寸比例的透明叠加层）。</summary>
public sealed record LauncherBackgroundFiles(string ImagePath, string? SloganPath, string Region, string Language);

/// <summary>
/// 窗口默认背景取自游戏启动器（接口文档 <c>docs/API/Launcher.md</c>「启动器背景」）：
/// 先请求国际服（按界面语言），失败（网络、开关关闭、图片下载失败）再请求国服（只有 zh-Hans），都失败返回 null，由界面退回默认底纹。
/// 成功后图片缓存在 <c>Cache/LauncherBackground</c>，下次启动先显示缓存再后台刷新；地址未变时不重复下载。
/// </summary>
public sealed class LauncherBackgroundService(IHttpClientFactory factory, IConfigService config, IAppPaths paths, ILoggerService logger)
{
    /// <summary>国服背景配置只有简体中文。</summary>
    public const string CnLanguage = "zh-Hans";

    private const string ManifestName = "manifest.json";
    private const long MaxFileBytes = 32L * 1024 * 1024;

    /// <summary>单个配置请求的超时；图片下载另计。</summary>
    public TimeSpan RequestTimeout { get; init; } = TimeSpan.FromSeconds(10);

    public TimeSpan DownloadTimeout { get; init; } = TimeSpan.FromSeconds(45);

    private readonly SemaphoreSlim _gate = new(1, 1);

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true, WriteIndented = true };

    public string CacheDirectory => Path.Combine(paths.CacheDirectory, "LauncherBackground");

    /// <summary>读取上次成功下载的背景；文件缺失时返回 null。</summary>
    public LauncherBackgroundFiles? LoadCached()
    {
        Manifest? manifest = ReadManifest();
        if (manifest is null) return null;
        string image = Path.Combine(CacheDirectory, manifest.ImageFile);
        if (!File.Exists(image)) return null;
        string? slogan = manifest.SloganFile is { } s && File.Exists(Path.Combine(CacheDirectory, s)) ? Path.Combine(CacheDirectory, s) : null;
        return new LauncherBackgroundFiles(image, slogan, manifest.Region, manifest.Language);
    }

    /// <summary>
    /// 按「国际服（<paramref name="language"/>）→ 国服（zh-Hans）」顺序获取并缓存背景；全部失败返回 null（不删除已有缓存）。
    /// </summary>
    public async Task<LauncherBackgroundFiles?> RefreshAsync(string language, CancellationToken token = default)
    {
        await _gate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            var sources = new (string Region, string Url, string Language)[]
            {
                ("NET", Format(config.Api.Urls.LauncherBackgroundUrlNET, language), language),
                ("CN", Format(config.Api.Urls.LauncherBackgroundUrlCN, CnLanguage), CnLanguage),
            };
            foreach (var (region, url, lang) in sources)
            {
                token.ThrowIfCancellationRequested();
                if (string.IsNullOrWhiteSpace(url)) continue;
                try
                {
                    LauncherBackgroundFiles? files = await TryFetchAsync(region, url, lang, token).ConfigureAwait(false);
                    if (files is not null) return files;
                }
                catch (Exception ex) when (IsRecoverable(ex, token))
                {
                    logger.Warn($"启动器背景（{region}）获取失败：{ex.GetType().Name} {ex.Message}");
                }
            }
            logger.Info("启动器背景：国际服与国服均获取失败，使用默认背景");
            return null;
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<LauncherBackgroundFiles?> TryFetchAsync(string region, string url, string language, CancellationToken token)
    {
        HttpClient client = factory.CreateClient("WwToolClient");
        string json;
        using (var cts = CancellationTokenSource.CreateLinkedTokenSource(token))
        {
            cts.CancelAfter(RequestTimeout);
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            AddHeaders(request);
            using HttpResponseMessage response = await client.SendAsync(request, cts.Token).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            json = await response.Content.ReadAsStringAsync(cts.Token).ConfigureAwait(false);
        }

        BackgroundDto? dto = JsonSerializer.Deserialize<BackgroundDto>(json, JsonOptions);
        if (dto is null || dto.FunctionSwitch != 1)
        {
            logger.Info($"启动器背景（{region}）未启用（functionSwitch={dto?.FunctionSwitch}）");
            return null;
        }
        // 视频背景（类型 2）取首帧图；其他类型优先用背景文件本身，缺失时也退回首帧图。
        string? imageUrl = dto.BackgroundFileType == 2 ? dto.FirstFrameImage : dto.BackgroundFile ?? dto.FirstFrameImage;
        if (!IsHttpUrl(imageUrl))
        {
            logger.Info($"启动器背景（{region}）缺少可用的背景图片地址");
            return null;
        }
        string? sloganUrl = IsHttpUrl(dto.Slogan) ? dto.Slogan : null;

        Directory.CreateDirectory(CacheDirectory);
        Manifest? old = ReadManifest();
        string imageFile = await EnsureFileAsync(client, imageUrl!, "bg-", old?.ImageUrl == imageUrl ? old.ImageFile : null, token).ConfigureAwait(false);
        string? sloganFile = null;
        if (sloganUrl is not null)
        {
            try
            {
                sloganFile = await EnsureFileAsync(client, sloganUrl, "slogan-", old?.SloganUrl == sloganUrl ? old.SloganFile : null, token).ConfigureAwait(false);
            }
            catch (Exception ex) when (IsRecoverable(ex, token))
            {
                // 标语图只是装饰，下载失败不影响背景。
                logger.Warn($"启动器标语图（{region}）下载失败：{ex.GetType().Name}");
                sloganUrl = null;
            }
        }

        var manifest = new Manifest
        {
            Region = region,
            Language = language,
            ImageUrl = imageUrl!,
            ImageFile = imageFile,
            SloganUrl = sloganUrl,
            SloganFile = sloganFile,
            FetchedAtUtc = DateTimeOffset.UtcNow,
        };
        WriteManifest(manifest);
        CleanUp(manifest);
        logger.Info($"启动器背景已更新（{region}，{language}）");
        return new LauncherBackgroundFiles(Path.Combine(CacheDirectory, imageFile),
            sloganFile is null ? null : Path.Combine(CacheDirectory, sloganFile), region, language);
    }

    /// <summary>地址与已缓存的一致且文件仍在时直接复用，否则下载到临时文件后改名。</summary>
    private async Task<string> EnsureFileAsync(HttpClient client, string url, string prefix, string? cachedFile, CancellationToken token)
    {
        if (cachedFile is not null && File.Exists(Path.Combine(CacheDirectory, cachedFile))) return cachedFile;

        string name = prefix + SafeName(url);
        string target = Path.Combine(CacheDirectory, name);
        if (File.Exists(target)) return name;

        string temp = target + ".part";
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(token);
        cts.CancelAfter(DownloadTimeout);
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            AddHeaders(request);
            using HttpResponseMessage response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cts.Token).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            if (response.Content.Headers.ContentLength is > MaxFileBytes) throw new InvalidDataException("背景图片过大");
            await using (Stream source = await response.Content.ReadAsStreamAsync(cts.Token).ConfigureAwait(false))
            await using (FileStream file = File.Create(temp))
            {
                byte[] buffer = new byte[81920];
                long total = 0;
                int read;
                while ((read = await source.ReadAsync(buffer, cts.Token).ConfigureAwait(false)) > 0)
                {
                    total += read;
                    if (total > MaxFileBytes) throw new InvalidDataException("背景图片过大");
                    await file.WriteAsync(buffer.AsMemory(0, read), cts.Token).ConfigureAwait(false);
                }
                if (total == 0) throw new InvalidDataException("背景图片为空");
            }
            File.Move(temp, target, overwrite: true);
            return name;
        }
        finally
        {
            TryDelete(temp);
        }
    }

    private void AddHeaders(HttpRequestMessage request)
    {
        var headers = config.Api.CommonHeaders;
        if (!string.IsNullOrWhiteSpace(headers.UserAgent)) request.Headers.TryAddWithoutValidation("User-Agent", headers.UserAgent);
        request.Headers.TryAddWithoutValidation("Accept", "application/json, text/plain, */*");
    }

    /// <summary>删除不再引用的旧图片与残留的临时文件。</summary>
    private void CleanUp(Manifest manifest)
    {
        try
        {
            foreach (string file in Directory.EnumerateFiles(CacheDirectory))
            {
                string name = Path.GetFileName(file);
                if (name == ManifestName || name == manifest.ImageFile || name == manifest.SloganFile) continue;
                TryDelete(file);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.Debug($"启动器背景缓存清理失败：{ex.GetType().Name}");
        }
    }

    private Manifest? ReadManifest()
    {
        string path = Path.Combine(CacheDirectory, ManifestName);
        try
        {
            if (!File.Exists(path)) return null;
            Manifest? manifest = JsonSerializer.Deserialize<Manifest>(File.ReadAllText(path), JsonOptions);
            return manifest is { ImageFile.Length: > 0 } && SafeRelative(manifest.ImageFile) && (manifest.SloganFile is null || SafeRelative(manifest.SloganFile))
                ? manifest : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return null;
        }
    }

    private void WriteManifest(Manifest manifest)
    {
        string path = Path.Combine(CacheDirectory, ManifestName);
        string temp = path + ".part";
        File.WriteAllText(temp, JsonSerializer.Serialize(manifest, JsonOptions));
        File.Move(temp, path, overwrite: true);
    }

    private static bool SafeRelative(string name) =>
        name == Path.GetFileName(name) && name.IndexOfAny(Path.GetInvalidFileNameChars()) < 0;

    /// <summary>取地址最后一段作文件名（资源名本身是唯一的哈希）；不合法时用地址的哈希。</summary>
    private static string SafeName(string url)
    {
        string last = Path.GetFileName(new Uri(url).AbsolutePath);
        if (last.Length is > 0 and <= 120 && SafeRelative(last) && Path.HasExtension(last)) return last;
        byte[] hash = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(url));
        return Convert.ToHexString(hash, 0, 16).ToLowerInvariant() + Path.GetExtension(last);
    }

    private static string Format(string template, string language) =>
        string.IsNullOrWhiteSpace(template) ? string.Empty : string.Format(CultureInfo.InvariantCulture, template, Uri.EscapeDataString(language));

    private static bool IsHttpUrl(string? url) =>
        Uri.TryCreate(url, UriKind.Absolute, out Uri? uri) && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp);

    private static bool IsRecoverable(Exception ex, CancellationToken token) =>
        ex is HttpRequestException or JsonException or IOException or UnauthorizedAccessException or InvalidDataException or UriFormatException or FormatException
        || ex is OperationCanceledException && !token.IsCancellationRequested;

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }

    private sealed class BackgroundDto
    {
        public int FunctionSwitch { get; set; }
        public string? BackgroundFile { get; set; }
        public int BackgroundFileType { get; set; }
        public string? FirstFrameImage { get; set; }
        public string? Slogan { get; set; }
    }

    private sealed class Manifest
    {
        public string Region { get; set; } = string.Empty;
        public string Language { get; set; } = string.Empty;
        public string ImageUrl { get; set; } = string.Empty;
        public string ImageFile { get; set; } = string.Empty;
        public string? SloganUrl { get; set; }
        public string? SloganFile { get; set; }
        public DateTimeOffset FetchedAtUtc { get; set; }
    }
}
