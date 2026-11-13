namespace WwTool.Core.Services.Notices;

/// <summary>循环活动的刷新方式。</summary>
public enum RecurringKind
{
    /// <summary>每周一 04:00 刷新。</summary>
    Weekly,

    /// <summary>从 <see cref="RecurringActivity.Anchor"/> 起每 <see cref="RecurringActivity.EveryDays"/> 天的 04:00 刷新。</summary>
    Interval,

    /// <summary>跟随版本周期，版本结束时刷新。</summary>
    Version,
}

/// <summary>一项按固定规则刷新的玩法（不在公告里，由规则推算）。</summary>
public sealed record RecurringActivity(string Key, RecurringKind Kind, DateOnly? Anchor = null, int EveryDays = 0);

/// <summary>循环玩法当前这一轮的起止时间。</summary>
public sealed record RecurringWindow(RecurringActivity Activity, DateTimeOffset Start, DateTimeOffset End);

/// <summary>
/// 鸣潮的循环玩法：逆境深塔、冥歌海墟每 28 天一轮，周本每周一刷新，商店兑换随版本刷新。
/// 刷新时刻为服务器时间 04:00（按 UTC+8 推算；锚点日期与 3.8 版本官方日历一致）。
/// </summary>
public static class RecurringActivities
{
    private static readonly TimeSpan Offset = TimeSpan.FromHours(8);
    private static readonly TimeSpan ResetTime = TimeSpan.FromHours(4);

    public static IReadOnlyList<RecurringActivity> WutheringWaves { get; } =
    [
        new("tower", RecurringKind.Interval, new DateOnly(2026, 4, 27), 28),
        new("wastes", RecurringKind.Interval, new DateOnly(2026, 4, 13), 28),
        new("weekly", RecurringKind.Weekly),
        new("shop", RecurringKind.Version),
    ];

    /// <summary>
    /// 包含 <paramref name="now"/> 的那一轮；版本类玩法没有版本周期或已不在周期内时返回 null。
    /// </summary>
    public static RecurringWindow? Current(RecurringActivity activity, DateTimeOffset now, DateTimeOffset? versionStart = null, DateTimeOffset? versionEnd = null)
    {
        switch (activity.Kind)
        {
            case RecurringKind.Weekly:
            {
                DateTimeOffset local = now.ToOffset(Offset);
                int sinceMonday = ((int)local.DayOfWeek + 6) % 7;
                var start = new DateTimeOffset(local.Date.AddDays(-sinceMonday) + ResetTime, Offset);
                if (start > now) start = start.AddDays(-7);
                return new RecurringWindow(activity, start, start.AddDays(7));
            }
            case RecurringKind.Interval when activity.Anchor is { } anchor && activity.EveryDays > 0:
            {
                var first = new DateTimeOffset(anchor.ToDateTime(TimeOnly.MinValue) + ResetTime, Offset);
                double cycles = Math.Floor((now - first).TotalDays / activity.EveryDays);
                DateTimeOffset start = first.AddDays(cycles * activity.EveryDays);
                return new RecurringWindow(activity, start, start.AddDays(activity.EveryDays));
            }
            case RecurringKind.Version when versionStart is { } vs && versionEnd is { } ve && vs <= now && now < ve:
                return new RecurringWindow(activity, vs, ve);
            default:
                return null;
        }
    }
}
