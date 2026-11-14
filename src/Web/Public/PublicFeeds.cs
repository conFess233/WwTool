using System.Collections.Concurrent;
using WwTool.Core.Abstractions;
using WwTool.Core.Services;
using WwTool.Core.Services.Challenges;
using WwTool.Core.Services.Notices;

namespace WwTool.Web.Public;

/// <summary>
/// 公开数据的共用缓存：同一键在 <c>ttl</c> 内只向上游请求一次，并发请求共用同一次刷新；
/// 刷新失败时保留上一次的结果，首次请求先用 Core 的磁盘缓存兜底。
/// </summary>
public sealed class PublicFeedCache(ILoggerService logger)
{
    private sealed class Slot
    {
        public readonly SemaphoreSlim Gate = new(1, 1);
        public object? Value;
        public DateTimeOffset CheckedAt = DateTimeOffset.MinValue;
    }

    private readonly ConcurrentDictionary<string, Slot> _slots = new();

    public async Task<T?> GetAsync<T>(string key, TimeSpan ttl, Func<T?> loadCached, Func<Task<T?>> refresh, CancellationToken token) where T : class
    {
        Slot slot = _slots.GetOrAdd(key, _ => new Slot());
        if (slot.Value is T hit && DateTimeOffset.UtcNow - slot.CheckedAt < ttl) return hit;

        await slot.Gate.WaitAsync(token);
        try
        {
            if (slot.Value is T again && DateTimeOffset.UtcNow - slot.CheckedAt < ttl) return again;
            if (slot.Value is null)
            {
                try { slot.Value = loadCached(); }
                catch (Exception ex) when (ex is IOException or System.Text.Json.JsonException or FormatException) { logger.Warn($"公开数据缓存读取失败（{key}）：{ex.GetType().Name}"); }
            }
            try
            {
                if (await refresh() is { } fresh) slot.Value = fresh;
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException or System.Text.Json.JsonException or FormatException)
            {
                logger.Warn($"公开数据刷新失败（{key}）：{ex.GetType().Name}");
            }
            slot.CheckedAt = DateTimeOffset.UtcNow;
            return slot.Value as T;
        }
        finally
        {
            slot.Gate.Release();
        }
    }
}

public sealed record RedeemCodesDto(string? Version, IReadOnlyList<string> Codes, DateTimeOffset? ExpiresAt, DateTimeOffset PostedAt, string PostUrl);
public sealed record LivestreamPreviewDto(string? Version, DateTimeOffset StartsAt, string Title, string PostUrl);
public sealed record LivestreamDto(IReadOnlyList<RedeemCodesDto> Codes, LivestreamPreviewDto? Preview, DateTimeOffset FetchedAt);

public sealed record SeasonDto(string Kind, int Id, string Name, DateTimeOffset? Start, DateTimeOffset? End, string? SeasonName, string? EndVersion);
public sealed record SeasonsDto(IReadOnlyList<SeasonDto> Seasons, int? CurrentId, string PageUrl, DateTimeOffset FetchedAt);
public sealed record ChallengeDetailDto(string Kind, int Id, object Detail, DateTimeOffset FetchedAt);
public sealed record CharacterDetailDto(EncoreCharacter Character, string PageUrl, DateTimeOffset FetchedAt);

public sealed record RecurringDto(string Key, string Kind, int EveryDays, DateTimeOffset Start, DateTimeOffset End);

public static class PublicFeedEndpoints
{
    private static readonly TimeSpan CodesTtl = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan ChallengeTtl = TimeSpan.FromMinutes(30);

    /// <summary>当前时刻的循环玩法（逆境深塔、冥歌海墟、周本、商店），规则见 <see cref="RecurringActivities"/>。</summary>
    public static IReadOnlyList<RecurringDto> Recurring(DateTimeOffset? versionStart, DateTimeOffset? versionEnd)
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        return RecurringActivities.WutheringWaves
            .Select(a => RecurringActivities.Current(a, now, versionStart, versionEnd))
            .OfType<RecurringWindow>()
            .Select(w => new RecurringDto(w.Activity.Key, w.Activity.Kind.ToString(), w.Activity.EveryDays, w.Start, w.End))
            .ToList();
    }

    public static void MapPublicFeeds(this IEndpointRouteBuilder pub)
    {
        // 库街区「前瞻通讯」兑换码与直播预告（只有国服社区）
        pub.MapGet("/livestream", async (KuroBbsCodeService codes, PublicFeedCache cache, CancellationToken token) =>
        {
            LivestreamFeed? feed = await cache.GetAsync("livestream", CodesTtl, codes.LoadCached, () => codes.RefreshAsync(CancellationToken.None), token);
            return feed is null
                ? Results.Problem("兑换码暂时无法获取", statusCode: StatusCodes.Status503ServiceUnavailable)
                : Results.Ok(new LivestreamDto(
                    feed.Codes.Select(c => new RedeemCodesDto(c.Version, c.Codes, c.ExpiresAt, c.PostedAt, c.PostUrl)).ToList(),
                    feed.Preview is { } p ? new LivestreamPreviewDto(p.Version, p.StartsAt, p.Title, p.PostUrl) : null,
                    feed.FetchedAtUtc));
        });

        // 逆境深塔 / 冥歌海墟 / 终焉矩阵（encore.moe 解包数据）
        pub.MapGet("/challenges/{kind}", async (string kind, string? lang, EncoreService encore, PublicFeedCache cache, CancellationToken token) =>
        {
            if (!TryKind(kind, out ChallengeKind k)) return Results.NotFound();
            string language = EncoreService.RequestLanguage(lang ?? "zh-Hans");
            var data = await cache.GetAsync($"seasons|{k}|{language}", ChallengeTtl,
                () => encore.LoadSeasons(k, language), () => encore.RefreshSeasonsAsync(k, language, CancellationToken.None), token);
            if (data is null) return Results.Problem("挑战数据暂时无法获取", statusCode: StatusCodes.Status503ServiceUnavailable);
            ChallengeSeason? current = EncoreParser.Current(data.Value, DateTimeOffset.UtcNow);
            return Results.Ok(new SeasonsDto(
                data.Value.Select(s => new SeasonDto(s.Kind.ToString(), s.Id, s.Name, s.Start, s.End, s.SeasonName, s.EndVersion)).ToList(),
                current?.Id, encore.PageUrl(k, language), data.FetchedAtUtc));
        });

        pub.MapGet("/challenges/{kind}/{id:int}", async (string kind, int id, string? lang, EncoreService encore, PublicFeedCache cache, CancellationToken token) =>
        {
            if (!TryKind(kind, out ChallengeKind k) || id is < 0 or > 100_000) return Results.NotFound();
            string language = EncoreService.RequestLanguage(lang ?? "zh-Hans");
            // 只接受列表里存在的期数，避免任意 ID 穿透到上游
            var seasons = await cache.GetAsync($"seasons|{k}|{language}", ChallengeTtl,
                () => encore.LoadSeasons(k, language), () => encore.RefreshSeasonsAsync(k, language, CancellationToken.None), token);
            if (seasons is null || seasons.Value.All(s => s.Id != id)) return Results.NotFound();
            var data = await cache.GetAsync($"detail|{k}|{id}|{language}", ChallengeTtl,
                () => encore.LoadDetail(k, id, language), () => encore.RefreshDetailAsync(k, id, language, CancellationToken.None), token);
            return data is null
                ? Results.Problem("挑战数据暂时无法获取", statusCode: StatusCodes.Status503ServiceUnavailable)
                : Results.Ok(new ChallengeDetailDto(k.ToString(), id, data.Value, data.FetchedAtUtc));
        });

        // 角色详情（encore.moe）：只接受游戏资料里的角色 ID，避免任意 ID 穿透到上游
        pub.MapGet("/characters/{id:int}", async (int id, string? lang, EncoreService encore, GameDataService catalog, PublicFeedCache cache, CancellationToken token) =>
        {
            if (catalog.GetItemById(id) is not { Type: "Character" }) return Results.NotFound();
            string language = EncoreService.RequestLanguage(lang ?? "zh-Hans");
            var data = await cache.GetAsync($"character|{id}|{language}", CharacterTtl,
                () => encore.LoadCharacter(id, language), () => encore.RefreshCharacterAsync(id, language, CancellationToken.None), token);
            return data is null
                ? Results.Problem("角色资料暂时无法获取", statusCode: StatusCodes.Status503ServiceUnavailable)
                : Results.Ok(new CharacterDetailDto(data.Value, encore.CharacterPageUrl(id, language), data.FetchedAtUtc));
        });
    }

    private static readonly TimeSpan CharacterTtl = TimeSpan.FromHours(12);

    private static readonly TimeSpan BackgroundTtl = TimeSpan.FromMinutes(30);

    /// <summary>
    /// 启动器背景（与桌面端「默认背景」相同）：后端只读取启动器的背景配置，返回图片、标语图与视频的原始地址，
    /// 由浏览器直接从启动器 CDN 加载，服务器不下载、不转发这些文件。
    /// </summary>
    public static void MapBackground(this IEndpointRouteBuilder pub)
    {
        pub.MapGet("/background", async (string? lang, LauncherBackgroundService background, PublicFeedCache cache, CancellationToken token) =>
        {
            string language = lang is "en" or "ja" ? lang : "zh-Hans";
            LauncherBackgroundLinks? links = await cache.GetAsync($"background|{language}", BackgroundTtl,
                () => null, () => background.ResolveLinksAsync(language, CancellationToken.None), token);
            return links is null
                ? Results.NotFound()
                : Results.Ok(new { image = links.ImageUrl, slogan = links.SloganUrl, video = links.VideoUrl, region = links.Region });
        });
    }

    private static readonly TimeSpan NewsTtl = TimeSpan.FromMinutes(10);

    /// <summary>首页「游戏公告」：启动器资讯（活动 / 公告 / 新闻）与轮播图，国服、国际服分别缓存；轮播图只给原始地址。</summary>
    public static void MapLauncherNews(this IEndpointRouteBuilder pub)
    {
        pub.MapGet("/launcher-news", async (string? region, string? lang, LauncherNewsService news, PublicFeedCache cache, CancellationToken token) =>
        {
            LauncherRegion r = string.Equals(region, "china", StringComparison.OrdinalIgnoreCase) ? LauncherRegion.China : LauncherRegion.Global;
            string language = lang is "en" or "ja" ? lang : "zh-Hans";
            LauncherNews? feed = await cache.GetAsync($"news|{r}|{LauncherNewsService.RequestLanguage(r, language)}", NewsTtl,
                () => news.LoadCachedLinks(r, language),
                async () => await news.RefreshAsync(r, language, CancellationToken.None) is null ? null : news.LoadCachedLinks(r, language),
                token);
            if (feed is null) return Results.Problem("资讯暂时无法获取", statusCode: StatusCodes.Status503ServiceUnavailable);
            return Results.Ok(new
            {
                region = NoticeMapper.RegionKey(feed.Region),
                items = feed.Items.Select(i => new { category = i.Category.ToString(), i.Title, link = i.Link?.ToString(), i.Date, i.DateText }),
                slides = feed.Slides.Where(sl => sl.ImageUrl is not null).Select(sl => new { image = sl.ImageUrl!.ToString(), link = sl.Link?.ToString() }),
                fetchedAt = feed.FetchedAtUtc,
            });
        });
    }

    private static bool TryKind(string text, out ChallengeKind kind) =>
        Enum.TryParse(text, ignoreCase: true, out kind) && Enum.IsDefined(kind);
}
