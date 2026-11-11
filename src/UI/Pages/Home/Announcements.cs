using Avalonia.Media;
using WwTool.Core.Services;

namespace WwTool.UI.Pages.Home;

/// <summary>公告类型：与游戏启动器资讯的三个栏目对应，决定标签颜色与筛选分组。</summary>
public enum AnnouncementKind
{
    Event,
    Notice,
    News,
}

/// <summary>公告列表的一行。<see cref="Date"/> 为空时日期按原文显示（<see cref="DateText"/>）。</summary>
public sealed record Announcement(AnnouncementKind Kind, string Title, DateTime? Date, Uri? Link = null, string? DateText = null);

/// <summary>
/// 横幅轮播的一张。启动器轮播图为整张图片（<see cref="LocalImage"/>），点击打开 <see cref="Link"/>；
/// 没有图片时按定稿画渐变底与状态词、标题、日期（调试用示例数据）。
/// </summary>
public sealed record AnnouncementBanner(
    AnnouncementKind Kind,
    string Status,
    string Title,
    string DateText,
    string? ImagePath = null,
    string? LocalImage = null,
    Uri? Link = null)
{
    /// <summary>横幅底色渐变（开发文档「游戏公告卡 → 横幅」）。</summary>
    public IReadOnlyList<Color> Gradient => Kind switch
    {
        AnnouncementKind.News => [Color.Parse("#1F2A44"), Color.Parse("#2F5E9E")],
        AnnouncementKind.Notice => [Color.Parse("#5A2418"), Color.Parse("#A8482A")],
        _ => [Color.Parse("#2B2620"), Color.Parse("#5A4426"), Color.Parse("#9A6A14")],
    };

    public static AnnouncementBanner FromImage(string path, Uri? link) => new(AnnouncementKind.Event, string.Empty, string.Empty, string.Empty, LocalImage: path, Link: link);
}

/// <summary>公告数据。</summary>
public sealed record AnnouncementFeed(IReadOnlyList<AnnouncementBanner> Banners, IReadOnlyList<Announcement> Items, DateTimeOffset? FetchedAtUtc = null)
{
    public static AnnouncementFeed Empty { get; } = new([], []);

    public bool IsEmpty => Banners.Count == 0 && Items.Count == 0;
}

/// <summary>公告数据来源：先读缓存立即显示，再从接口刷新。</summary>
public interface IAnnouncementSource
{
    /// <summary>上次缓存的公告；没有时返回 null。</summary>
    AnnouncementFeed? LoadCached(LauncherRegion region, string language);

    /// <summary>从接口获取文字内容（轮播图只带已缓存的）；失败返回 null。</summary>
    Task<AnnouncementFeed?> RefreshAsync(LauncherRegion region, string language, CancellationToken token = default);

    /// <summary>后台下载还没有的轮播图；有新图时返回更新后的公告，否则返回 null。</summary>
    Task<AnnouncementFeed?> DownloadImagesAsync(LauncherRegion region, string language, CancellationToken token = default);
}

/// <summary>游戏启动器资讯（活动 / 公告 / 新闻与轮播图），国服、国际服分别获取。</summary>
public sealed class LauncherAnnouncementSource(LauncherNewsService news) : IAnnouncementSource
{
    public AnnouncementFeed? LoadCached(LauncherRegion region, string language)
    {
#if DEBUG
        if (Environment.GetEnvironmentVariable("WWTOOL_SAMPLE_ANNOUNCEMENTS") == "1") return Sample();
#endif
        return news.LoadCached(region, language) is { } cached ? ToFeed(cached) : null;
    }

    public async Task<AnnouncementFeed?> RefreshAsync(LauncherRegion region, string language, CancellationToken token = default)
    {
#if DEBUG
        if (Environment.GetEnvironmentVariable("WWTOOL_SAMPLE_ANNOUNCEMENTS") == "1") return Sample();
#endif
        return await news.RefreshAsync(region, language, token).ConfigureAwait(false) is { } fresh ? ToFeed(fresh) : null;
    }

    public async Task<AnnouncementFeed?> DownloadImagesAsync(LauncherRegion region, string language, CancellationToken token = default)
    {
#if DEBUG
        if (Environment.GetEnvironmentVariable("WWTOOL_SAMPLE_ANNOUNCEMENTS") == "1") return null;
#endif
        return await news.DownloadSlidesAsync(region, language, token).ConfigureAwait(false) is { } updated ? ToFeed(updated) : null;
    }

    private static AnnouncementFeed ToFeed(LauncherNews news) => new(
        [.. news.Slides.Select(s => AnnouncementBanner.FromImage(s.ImagePath, s.Link))],
        [.. news.Items.Select(i => new Announcement(Kind(i.Category), i.Title, i.Date, i.Link, i.DateText))],
        news.FetchedAtUtc);

    private static AnnouncementKind Kind(LauncherNewsCategory category) => category switch
    {
        LauncherNewsCategory.Activity => AnnouncementKind.Event,
        LauncherNewsCategory.News => AnnouncementKind.News,
        _ => AnnouncementKind.Notice,
    };

#if DEBUG
    /// <summary>本地对照定稿截图用的示例数据，只在调试版且显式开启时出现。</summary>
    private static AnnouncementFeed Sample()
    {
        int year = DateTime.Today.Year;
        DateTime D(int m, int d) => new(year, m, d);
        return new AnnouncementFeed(
            [
                new(AnnouncementKind.Event, "正在进行", "今汐 限时复刻唤取", "11/06 至 11/27"),
                new(AnnouncementKind.News, "版本更新", "3.7 版本更新说明", "11/06"),
                new(AnnouncementKind.Notice, "维护预告", "11 月 13 日停服维护", "06:00 至 11:00"),
            ],
            [
                new(AnnouncementKind.Event, "角色活动唤取：今汐 限时复刻开启", D(11, 6)),
                new(AnnouncementKind.News, "3.7 版本更新说明", D(11, 6)),
                new(AnnouncementKind.Notice, "11 月 13 日 06:00 停服维护预告", D(11, 11)),
                new(AnnouncementKind.Event, "先约电台新赛季开启", D(11, 6)),
                new(AnnouncementKind.Event, "武器活动唤取：时和岁稔", D(11, 6)),
                new(AnnouncementKind.Notice, "关于谨防账号交易诈骗的提醒", D(11, 2)),
                new(AnnouncementKind.News, "声骸系统调整说明", D(10, 30)),
                new(AnnouncementKind.Event, "社区创作征集活动开启", D(10, 28)),
            ]);
    }
#endif
}
