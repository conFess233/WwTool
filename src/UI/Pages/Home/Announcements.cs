using Avalonia.Media;

namespace WwTool.UI.Pages.Home;

/// <summary>公告类型：决定标签颜色、横幅底色与筛选分组。</summary>
public enum AnnouncementKind
{
    Event,
    Version,
    Maintenance,
    Notice,
}

/// <summary>公告列表的一行。</summary>
public sealed record Announcement(AnnouncementKind Kind, string Title, DateTime Date, Uri? Link = null);

/// <summary>横幅轮播的一张：状态词、标题、日期文字与右侧圆形角色图（攻略站缓存的相对路径，可为空）。</summary>
public sealed record AnnouncementBanner(AnnouncementKind Kind, string Status, string Title, string DateText, string? ImagePath = null)
{
    /// <summary>横幅底色渐变（开发文档「游戏公告卡 → 横幅」）。</summary>
    public IReadOnlyList<Color> Gradient => Kind switch
    {
        AnnouncementKind.Version => [Color.Parse("#1F2A44"), Color.Parse("#2F5E9E")],
        AnnouncementKind.Maintenance => [Color.Parse("#5A2418"), Color.Parse("#A8482A")],
        _ => [Color.Parse("#2B2620"), Color.Parse("#5A4426"), Color.Parse("#9A6A14")],
    };
}

/// <summary>公告数据。</summary>
public sealed record AnnouncementFeed(IReadOnlyList<AnnouncementBanner> Banners, IReadOnlyList<Announcement> Items, Uri? MoreLink)
{
    public static AnnouncementFeed Empty { get; } = new([], [], null);
}

/// <summary>
/// 公告数据来源。数据接口尚未确定（开发文档「待定事项」），默认实现返回空，首页显示空状态。
/// </summary>
public interface IAnnouncementSource
{
    Task<AnnouncementFeed> LoadAsync(string language, CancellationToken token = default);
}

/// <summary>尚未接入数据来源时的默认实现。</summary>
public sealed class EmptyAnnouncementSource : IAnnouncementSource
{
    public Task<AnnouncementFeed> LoadAsync(string language, CancellationToken token = default)
    {
#if DEBUG
        // 本地对照定稿截图用的示例数据，只在调试版且显式开启时出现。
        if (Environment.GetEnvironmentVariable("WWTOOL_SAMPLE_ANNOUNCEMENTS") == "1") return Task.FromResult(Sample());
#endif
        return Task.FromResult(AnnouncementFeed.Empty);
    }

#if DEBUG
    private static AnnouncementFeed Sample()
    {
        int year = DateTime.Today.Year;
        DateTime D(int m, int d) => new(year, m, d);
        return new AnnouncementFeed(
            [
                new(AnnouncementKind.Event, "正在进行", "今汐 限时复刻唤取", "11/06 至 11/27"),
                new(AnnouncementKind.Version, "版本更新", "3.7 版本更新说明", "11/06"),
                new(AnnouncementKind.Maintenance, "维护预告", "11 月 13 日停服维护", "06:00 至 11:00"),
            ],
            [
                new(AnnouncementKind.Event, "角色活动唤取：今汐 限时复刻开启", D(11, 6)),
                new(AnnouncementKind.Version, "3.7 版本更新说明", D(11, 6)),
                new(AnnouncementKind.Maintenance, "11 月 13 日 06:00 停服维护预告", D(11, 11)),
                new(AnnouncementKind.Event, "先约电台新赛季开启", D(11, 6)),
                new(AnnouncementKind.Event, "武器活动唤取：时和岁稔", D(11, 6)),
                new(AnnouncementKind.Notice, "关于谨防账号交易诈骗的提醒", D(11, 2)),
                new(AnnouncementKind.Version, "声骸系统调整说明", D(10, 30)),
                new(AnnouncementKind.Event, "社区创作征集活动开启", D(10, 28)),
            ],
            null);
    }
#endif
}
