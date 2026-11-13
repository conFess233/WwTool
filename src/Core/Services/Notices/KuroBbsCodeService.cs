using System.Globalization;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using WwTool.Core.Abstractions;
using WwTool.Core.Abstractions.Platform;
using WwTool.Core.Composition;

namespace WwTool.Core.Services.Notices;

/// <summary>一次前瞻直播的兑换码。</summary>
/// <param name="ExpiresAt">有效期截止；正文没写时为 null。</param>
public sealed record RedeemCodeSet(string? Version, IReadOnlyList<string> Codes, DateTimeOffset? ExpiresAt, DateTimeOffset PostedAt, string PostUrl)
{
    public bool IsExpired(DateTimeOffset now) => ExpiresAt is { } at && at <= now;
}

/// <summary>官方预告的前瞻直播。</summary>
public sealed record LivestreamPreview(string? Version, DateTimeOffset StartsAt, string Title, string PostUrl);

public sealed record LivestreamFeed(IReadOnlyList<RedeemCodeSet> Codes, LivestreamPreview? Preview, DateTimeOffset FetchedAtUtc);

/// <summary>
/// 库街区官方「前瞻通讯」帖子里的兑换码与直播预告（接口文档 <c>docs/API/KuroBBS_Post.md</c>）。只有国服社区，
/// 结果缓存在 <c>Cache/GameNotice/kurobbs.json</c>。
/// </summary>
public sealed partial class KuroBbsCodeService(IHttpClientFactory factory, IConfigService config, IAppPaths paths, ILoggerService logger)
{
    public const string OfficialUserId = "10012001";
    private const int GameId = 3;
    private const string Keyword = "前瞻通讯";
    private const int MaxDetails = 3;
    private static readonly TimeSpan SourceOffset = TimeSpan.FromHours(8);
    private static readonly TimeSpan MaxPostAge = TimeSpan.FromDays(60);

    public TimeSpan RequestTimeout { get; init; } = TimeSpan.FromSeconds(10);
    public Func<DateTimeOffset> Now { get; init; } = () => DateTimeOffset.UtcNow;

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public string CachePath => Path.Combine(paths.CacheDirectory, "GameNotice", "kurobbs.json");

    [GeneratedRegex(@"[【\[「]\s*([A-Z0-9]{6,24})\s*[】\]」]")]
    private static partial Regex BracketCodeRegex();

    [GeneratedRegex(@"(?<![A-Za-z0-9])[A-Z0-9]{8,24}(?![A-Za-z0-9])")]
    private static partial Regex LooseCodeRegex();

    [GeneratedRegex(@"(?:(?<y>\d{4})\s*[年/.\-]\s*)?(?<mo>\d{1,2})\s*[月/.\-]\s*(?<d>\d{1,2})\s*日?\s*(?<h>\d{1,2})\s*(?:[:：]\s*(?<mi>\d{2})(?:\s*[:：]\s*(?<s>\d{2}))?|[时点])")]
    private static partial Regex DateTimeRegex();

    [GeneratedRegex(@"失效|有效期|截止|过期|兑换期限|前兑换|前使用|前领取")]
    private static partial Regex ExpiryContextRegex();

    [GeneratedRegex(@"(\d+(?:\.\d+)+)\s*版本")]
    private static partial Regex VersionRegex();

    public LivestreamFeed? LoadCached()
    {
        try
        {
            return File.Exists(CachePath) ? JsonSerializer.Deserialize<LivestreamFeed>(File.ReadAllText(CachePath), JsonOptions) : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or NotSupportedException)
        {
            return null;
        }
    }

    /// <summary>搜索官方前瞻帖并读取兑换码；失败返回 null（保留旧缓存）。</summary>
    public async Task<LivestreamFeed?> RefreshAsync(CancellationToken token = default)
    {
        DateTimeOffset now = Now();
        JsonElement? search = await PostAsync(config.Api.Urls.KuroBbsSearchUrl, new Dictionary<string, string>
        {
            ["keyword"] = Keyword,
            ["gameId"] = GameId.ToString(CultureInfo.InvariantCulture),
            ["pageIndex"] = "1",
            ["pageSize"] = "20",
        }, token).ConfigureAwait(false);
        if (search is not { } data || !data.TryGetProperty("postList", out JsonElement list) || list.ValueKind != JsonValueKind.Array) return null;

        var posts = new List<(string Id, string Title, DateTimeOffset Created)>();
        foreach (JsonElement post in list.EnumerateArray())
        {
            if (Str(post, "userId") != OfficialUserId || Str(post, "postId") is not { Length: > 0 } id) continue;
            string title = (Str(post, "postTitle") ?? string.Empty).Replace("<em>", "", StringComparison.Ordinal).Replace("</em>", "", StringComparison.Ordinal);
            if (!title.Contains("前瞻", StringComparison.Ordinal)) continue;
            if (!long.TryParse(Str(post, "createTimestamp"), NumberStyles.Integer, CultureInfo.InvariantCulture, out long ms)) continue;
            DateTimeOffset created = DateTimeOffset.FromUnixTimeMilliseconds(ms);
            if (now - created > MaxPostAge) continue;
            posts.Add((id, title, created));
        }
        posts = posts.OrderByDescending(p => p.Created).ToList();

        // 直播预告：「x.x版本前瞻通讯将于2026年11月6日19:00正式播出」，取最新一条。
        LivestreamPreview? preview = null;
        foreach (var post in posts)
        {
            if (!post.Title.Contains("将于", StringComparison.Ordinal)) continue;
            if (ParseDate(post.Title, post.Created) is not { } at) continue;
            preview = new LivestreamPreview(Version(post.Title), at, post.Title, PostUrl(post.Id));
            break;
        }

        var codes = new List<RedeemCodeSet>();
        foreach (var post in posts.Where(p => p.Title.Contains(Keyword, StringComparison.Ordinal) && !p.Title.Contains("将于", StringComparison.Ordinal)).Take(MaxDetails))
        {
            JsonElement? detail = await PostAsync(config.Api.Urls.KuroBbsPostDetailUrl, new Dictionary<string, string>
            {
                ["postId"] = post.Id,
                ["showOrderType"] = "2",
                ["isOnlyPublisher"] = "1",
                ["pageIndex"] = "1",
                ["pageSize"] = "20",
            }, token).ConfigureAwait(false);
            if (detail is not { } d) continue;
            JsonElement postDetail = d.TryGetProperty("postDetail", out JsonElement pd) ? pd : default;
            DateTimeOffset posted = postDetail.ValueKind == JsonValueKind.Object && ParsePostTime(Str(postDetail, "postTime")) is { } pt ? pt : post.Created;
            var texts = new List<string>();
            if (postDetail.ValueKind == JsonValueKind.Object) texts.Add(JoinContent(postDetail, "postContent"));
            if (d.TryGetProperty("comment", out JsonElement comments) && comments.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement comment in comments.EnumerateArray())
                {
                    bool publisher = Str(comment, "isPublisher") == "1" || Str(comment, "userId") == OfficialUserId;
                    if (publisher) texts.Add(JoinContent(comment, "commentContent"));
                }
            }
            foreach (string text in texts.Where(t => t.Contains("兑换码", StringComparison.Ordinal)))
            {
                IReadOnlyList<string> found = ExtractCodes(text);
                if (found.Count == 0) continue;
                codes.Add(new RedeemCodeSet(Version(text) ?? Version(post.Title), found, ExtractExpiry(text, posted), posted, PostUrl(post.Id)));
                break;
            }
        }

        // 同一组兑换码只留最新一帖。
        codes = codes.GroupBy(c => string.Join(",", c.Codes.Order(StringComparer.Ordinal)))
            .Select(g => g.OrderByDescending(c => c.PostedAt).First())
            .OrderByDescending(c => c.PostedAt)
            .ToList();
        var feed = new LivestreamFeed(codes, preview, now);
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(CachePath)!);
            File.WriteAllText(CachePath + ".part", JsonSerializer.Serialize(feed, JsonOptions));
            File.Move(CachePath + ".part", CachePath, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.Debug($"兑换码缓存写入失败：{ex.GetType().Name}");
        }
        return feed;
    }

    private string PostUrl(string id) => string.Format(CultureInfo.InvariantCulture, config.Api.Urls.KuroBbsPostPageUrl, id);

    private static string? Version(string text) => VersionRegex().Match(text) is { Success: true } m ? m.Groups[1].Value : null;

    /// <summary>兑换码：优先取括号（【】[]「」）里的 6–24 位大写字母数字，否则取「兑换码」之后的 8–24 位；至少含一个字母。</summary>
    public static IReadOnlyList<string> ExtractCodes(string text)
    {
        var result = new List<string>();
        void Add(string code)
        {
            if (code.Any(char.IsAsciiLetterUpper) && !result.Contains(code)) result.Add(code);
        }
        foreach (Match m in BracketCodeRegex().Matches(text)) Add(m.Groups[1].Value);
        if (result.Count > 0) return result;
        int index = text.IndexOf("兑换码", StringComparison.Ordinal);
        if (index < 0) return result;
        foreach (Match m in LooseCodeRegex().Matches(text[index..])) Add(m.Value);
        return result;
    }

    /// <summary>有效期：「有效期至 / 失效 / 截止」附近的日期时间（UTC+8），年份省略时按发布时间推断。</summary>
    public static DateTimeOffset? ExtractExpiry(string text, DateTimeOffset posted)
    {
        foreach (Match m in DateTimeRegex().Matches(text))
        {
            int from = Math.Max(0, m.Index - 24);
            string context = text[from..Math.Min(text.Length, m.Index + m.Length + 12)];
            if (!ExpiryContextRegex().IsMatch(context)) continue;
            if (ToDate(m, posted) is { } at) return at;
        }
        return null;
    }

    /// <summary>标题或正文里的直播时间。</summary>
    public static DateTimeOffset? ParseDate(string text, DateTimeOffset posted) =>
        DateTimeRegex().Match(text) is { Success: true } m ? ToDate(m, posted) : null;

    private static DateTimeOffset? ToDate(Match m, DateTimeOffset posted)
    {
        int month = int.Parse(m.Groups["mo"].Value, CultureInfo.InvariantCulture);
        int day = int.Parse(m.Groups["d"].Value, CultureInfo.InvariantCulture);
        int hour = int.Parse(m.Groups["h"].Value, CultureInfo.InvariantCulture);
        int minute = m.Groups["mi"].Success ? int.Parse(m.Groups["mi"].Value, CultureInfo.InvariantCulture) : 0;
        int second = m.Groups["s"].Success ? int.Parse(m.Groups["s"].Value, CultureInfo.InvariantCulture) : 0;
        if (month is < 1 or > 12 || day < 1 || hour > 24 || minute > 59 || second > 59) return null;
        bool hasYear = m.Groups["y"].Success;
        int year = hasYear ? int.Parse(m.Groups["y"].Value, CultureInfo.InvariantCulture) : posted.ToOffset(SourceOffset).Year;
        DateTimeOffset? Make(int y) => day > DateTime.DaysInMonth(y, month)
            ? null
            : new DateTimeOffset(new DateTime(y, month, day).AddHours(hour).AddMinutes(minute).AddSeconds(second), SourceOffset);
        DateTimeOffset? value = Make(year);
        if (!hasYear && value is { } v && v < posted.AddDays(-1)) value = Make(year + 1);
        return value;
    }

    private static DateTimeOffset? ParsePostTime(string? text) =>
        DateTime.TryParseExact(text, ["yyyy-MM-dd HH:mm", "yyyy-MM-dd HH:mm:ss"], CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime local)
            ? new DateTimeOffset(local, SourceOffset)
            : null;

    private static string JoinContent(JsonElement owner, string name)
    {
        if (!owner.TryGetProperty(name, out JsonElement value)) return string.Empty;
        if (value.ValueKind == JsonValueKind.String) return value.GetString() ?? string.Empty;
        if (value.ValueKind != JsonValueKind.Array) return string.Empty;
        return string.Join("\n", value.EnumerateArray()
            .Where(b => b.ValueKind == JsonValueKind.Object)
            .Select(b => Str(b, "content")?.Trim())
            .Where(s => !string.IsNullOrEmpty(s)));
    }

    private async Task<JsonElement?> PostAsync(string url, Dictionary<string, string> form, CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(url)) return null;
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(token);
            cts.CancelAfter(RequestTimeout);
            using var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = new FormUrlEncodedContent(form) };
            // 接口要求带 source（缺少时 code 102）；devCode / version 需存在但不校验。
            request.Headers.TryAddWithoutValidation("source", "android");
            request.Headers.TryAddWithoutValidation("devCode", "wwtool");
            request.Headers.TryAddWithoutValidation("version", "2.2.0");
            HttpClient client = factory.CreateClient(CoreServiceCollectionExtensions.ApiClient);
            using HttpResponseMessage response = await client.SendAsync(request, cts.Token).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            string json = await response.Content.ReadAsStringAsync(cts.Token).ConfigureAwait(false);
            using JsonDocument doc = JsonDocument.Parse(json);
            JsonElement root = doc.RootElement;
            if (Str(root, "code") != "200" || !root.TryGetProperty("data", out JsonElement data) || data.ValueKind != JsonValueKind.Object)
            {
                logger.Debug($"库街区接口返回异常：{Str(root, "code")} {Str(root, "msg")}");
                return null;
            }
            return data.Clone();
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or IOException
                                   || ex is OperationCanceledException && !token.IsCancellationRequested)
        {
            logger.Warn($"库街区前瞻帖获取失败：{ex.GetType().Name} {ex.Message}");
            return null;
        }
    }

    private static string? Str(JsonElement e, string name)
    {
        if (e.ValueKind != JsonValueKind.Object || !e.TryGetProperty(name, out JsonElement v)) return null;
        return v.ValueKind switch
        {
            JsonValueKind.String => v.GetString(),
            JsonValueKind.Number => v.GetRawText(),
            JsonValueKind.True => "1",
            JsonValueKind.False => "0",
            _ => null,
        };
    }
}
