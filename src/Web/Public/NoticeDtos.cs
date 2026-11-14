using WwTool.Core.Services;
using WwTool.Core.Services.Notices;

namespace WwTool.Web.Public;

/// <summary>公开接口返回的公告（与 Core 的 <see cref="GameNoticeFeed"/> 显式映射，不直接暴露领域模型）。</summary>
public sealed record NoticeFeedDto(
    string Region,
    string Language,
    string? VersionName,
    string? VersionNumber,
    DateTimeOffset? VersionStart,
    DateTimeOffset? VersionEnd,
    DateTimeOffset FetchedAt,
    NoticeCalendarDto? Calendar,
    IReadOnlyList<NoticeDto> Notices);

public sealed record NoticeDto(
    string Id,
    string Title,
    string Kind,
    string Pool,
    DateTimeOffset Start,
    DateTimeOffset? End,
    string? TimeText,
    string? EndText,
    IReadOnlyList<string> Featured,
    string? PoolName,
    string? Banner,
    string Content,
    DateTimeOffset Updated);

public sealed record NoticeCalendarDto(
    string? BackgroundUrl,
    DateTimeOffset? VersionStart,
    DateTimeOffset? VersionEnd,
    IReadOnlyList<NoticeCalendarNodeDto> Nodes);

public sealed record NoticeCalendarNodeDto(string NoticeId, bool Published, double X1, double Y1, double X2, double Y2);

public static class NoticeMapper
{
    public static string RegionKey(LauncherRegion region) => region == LauncherRegion.China ? "china" : "global";

    public static NoticeFeedDto Map(GameNoticeFeed feed) => new(
        RegionKey(feed.Region),
        feed.Language,
        feed.VersionName,
        feed.VersionNumber,
        feed.VersionStart,
        feed.VersionEnd,
        feed.FetchedAtUtc,
        feed.Calendar is { } c
            ? new NoticeCalendarDto(c.BackgroundUrl, c.VersionStart, c.VersionEnd,
                c.Nodes.Select(n => new NoticeCalendarNodeDto(n.NoticeId, n.Published, n.X1, n.Y1, n.X2, n.Y2)).ToList())
            : null,
        feed.Notices.Select(n => new NoticeDto(
            n.Id, n.Title, n.Kind.ToString(), n.Pool.ToString(), n.Start, n.End, n.TimeText, n.EndText,
            n.Featured, n.PoolName, n.Banner, n.Content, n.Updated)).ToList());
}
