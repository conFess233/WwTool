namespace WwTool.Core.Services.Notices;

/// <summary>公告的用途分类（按 category / tag 与标题归类，见 <c>docs/API/GameNotice.md</c>）。</summary>
public enum NoticeKind
{
    /// <summary>版本内容说明、已知问题及更新说明等更新公告。</summary>
    Update,

    /// <summary>唤取（卡池）。</summary>
    Gacha,

    /// <summary>游戏内活动与玩法。</summary>
    Event,

    /// <summary>商城、礼包、服饰等付费内容。</summary>
    Shop,

    /// <summary>其他资讯：系统说明、社区、周边等。</summary>
    Info,
}

/// <summary>唤取类型。</summary>
public enum GachaPoolType
{
    None,
    Character,
    Weapon,

    /// <summary>角色忆旅唤取（复刻，可选 UP）。</summary>
    CharacterReverb,

    /// <summary>武器忆旅唤取。</summary>
    WeaponReverb,
}

/// <summary>一条游戏内公告。</summary>
/// <param name="Title">展示用标题（换行已替换为空格）。</param>
/// <param name="ShelfStart">公告上架时间（接口原值）。</param>
/// <param name="Start">活动真实开始时间：能从正文解析出来时用正文，否则同 <paramref name="ShelfStart"/>。</param>
/// <param name="End">活动真实结束时间；正文写「x.x 版本」这类相对说法时为 null，见 <paramref name="EndText"/>。</param>
/// <param name="TimeFromContent">起止时间来自正文解析（否则为上下架时间）。</param>
/// <param name="TimeText">正文里的时间原句（中文）。</param>
/// <param name="Featured">5 星 UP 的名字（按展示语言提取，取不到时为空）。</param>
/// <param name="PoolName">卡池名（标题里方括号 / 书名号中的部分）。</param>
public sealed record GameNotice(
    string Id,
    string Title,
    NoticeKind Kind,
    GachaPoolType Pool,
    int Category,
    int Tag,
    DateTimeOffset ShelfStart,
    DateTimeOffset ShelfEnd,
    DateTimeOffset Start,
    DateTimeOffset? End,
    bool TimeFromContent,
    string? TimeText,
    string? EndText,
    IReadOnlyList<string> Featured,
    string? PoolName,
    string? Banner,
    string Content,
    DateTimeOffset Updated)
{
    /// <summary>在 <paramref name="now"/> 时是否进行中。</summary>
    public bool IsActive(DateTimeOffset now) => Start <= now && (End is not { } end || now < end);

    public bool IsUpcoming(DateTimeOffset now) => Start > now;

    public bool IsEnded(DateTimeOffset now) => End is { } end && end <= now;
}

/// <summary>游戏内「版本日历」图上的一块可点区域（坐标为图片宽高的百分比）。</summary>
/// <param name="Published">关联公告已发布（能在公告列表里找到）；false 时为下半版本尚未发布的内容。</param>
public sealed record NoticeCalendarNode(string NoticeId, bool Published, double X1, double Y1, double X2, double Y2);

/// <summary>游戏内「版本日历」。</summary>
public sealed record NoticeCalendar(
    string Id,
    string? BackgroundUrl,
    DateTimeOffset? VersionStart,
    DateTimeOffset? VersionEnd,
    IReadOnlyList<NoticeCalendarNode> Nodes);

/// <summary>一次获取的游戏内公告。</summary>
/// <param name="VersionName">当前版本名（如「旧梦凋零的百合」），取自版本内容说明标题。</param>
/// <param name="VersionNumber">当前版本号（如 3.8）。</param>
public sealed record GameNoticeFeed(
    LauncherRegion Region,
    string Language,
    IReadOnlyList<GameNotice> Notices,
    NoticeCalendar? Calendar,
    string? VersionName,
    string? VersionNumber,
    DateTimeOffset? VersionStart,
    DateTimeOffset? VersionEnd,
    DateTimeOffset FetchedAtUtc)
{
    /// <summary>版本日历里还没有发布公告的节点数（下半版本的卡池与活动）。</summary>
    public int UnpublishedCount => Calendar?.Nodes.Count(n => !n.Published) ?? 0;
}
