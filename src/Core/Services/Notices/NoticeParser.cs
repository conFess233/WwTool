using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace WwTool.Core.Services.Notices;

/// <summary>
/// 把游戏内公告 JSON 解析为 <see cref="GameNoticeFeed"/>。
/// 分类、卡池类型与真实起止时间一律按简体中文版解析（各语言同一区服的公告 ID 与上下架时间一致，
/// 而正文时间写法随语言变化，日文版还换成了日本标准时间）；标题、正文、横幅和日历图用展示语言。
/// 规则说明见 <c>docs/API/GameNotice.md</c>。
/// </summary>
public static partial class NoticeParser
{
    /// <summary>公告正文时间的时区：国服为 UTC+8；国际服写「服务器时间」，同样按 UTC+8 解析。</summary>
    public static readonly TimeSpan SourceOffset = TimeSpan.FromHours(8);

    private static readonly string[] TimeLabels = ["活动时间", "唤取时间", "开放时间", "领取时间", "开启时间"];
    private static readonly string[] MaintenanceLabels = ["更新维护时间", "维护时间"];
    private static readonly string[] InfoTitleWords = ["签到工具", "反馈", "防沉迷", "公平运营", "社区资讯", "功能说明", "系统说明"];
    private static readonly string[] ShopTitleWords = ["礼包", "特惠", "商城", "限时上架", "折扣上架", "特别上架", "首充"];
    private static readonly string[] UpdateTitleWords = ["内容说明", "更新说明", "已知问题", "更新公告", "维护"];

    private const string DatePattern = @"(?:(?<y>\d{4})\s*[年/.\-]\s*)?(?<mo>\d{1,2})\s*[月/.\-]\s*(?<d>\d{1,2})\s*日?\s*(?<h>\d{1,2})\s*[:：]\s*(?<mi>\d{2})(?:\s*[:：]\s*(?<s>\d{2}))?";
    private const string VersionAfterPattern = @"(?<v>\d+(?:\.\d+)+)?\s*版本(?:更新|开启)后";
    private const string SeparatorPattern = @"\s*(?:~|～|-|至|到|—|–)\s*";

    [GeneratedRegex(@"(?:" + DatePattern + "|" + VersionAfterPattern + ")")]
    private static partial Regex EndpointRegex();

    [GeneratedRegex(DatePattern)]
    private static partial Regex DateRegex();

    [GeneratedRegex(@"(?<v>\d+(?:\.\d+)+)\s*版本\s*(?:~|～|-|至|到|—|–)\s*(?<v2>\d+(?:\.\d+)+)\s*版本")]
    private static partial Regex VersionRangeRegex();

    [GeneratedRegex(@"(\d+(?:\.\d+)+)\s*版本")]
    private static partial Regex VersionNumberRegex();

    [GeneratedRegex(@"[「“""『]([^「」”""『』]+)[」”""』]")]
    private static partial Regex QuotedRegex();

    [GeneratedRegex(@"[\[【「『]([^\]】」』]+)[\]】」』]")]
    private static partial Regex PoolNameRegex();

    /// <summary>
    /// 解析一次获取的结果。<paramref name="displayJson"/> 为展示语言版本；<paramref name="chineseJson"/> 为同一区服的简体中文版，
    /// 展示语言就是简体中文时可传 null（不可用时也传 null：此时时间退回上下架时间、分类只按 category / tag）。
    /// </summary>
    public static GameNoticeFeed Parse(LauncherRegion region, string language, string displayJson, string? chineseJson, DateTimeOffset fetchedAtUtc)
    {
        using JsonDocument display = JsonDocument.Parse(displayJson);
        using JsonDocument? chinese = chineseJson is null ? null : JsonDocument.Parse(chineseJson);
        List<RawNotice> shown = ReadNotices(display.RootElement);
        List<RawNotice> zh = chinese is not null ? ReadNotices(chinese.RootElement)
            : language == "zh-Hans" ? shown
            : [];
        var zhById = zh.GroupBy(n => n.Id).ToDictionary(g => g.Key, g => g.First());

        // 版本更新维护的结束时间 =「x.x 版本更新后」。
        Dictionary<string, DateTimeOffset> versionStarts = VersionStarts(zh);

        var notices = new List<GameNotice>();
        foreach (RawNotice item in shown.DistinctBy(n => n.Id))
        {
            zhById.TryGetValue(item.Id, out RawNotice? source);
            notices.Add(Build(item, source, language, versionStarts));
        }

        NoticeCalendar? calendar = ReadCalendar(display.RootElement, notices);
        (string? name, string? number, RawNotice? versionNotice) = CurrentVersion(zh.Count > 0 ? zh : shown, shown, fetchedAtUtc);
        DateTimeOffset? versionStart = calendar?.VersionStart ?? versionNotice?.Start;
        DateTimeOffset? versionEnd = calendar?.VersionEnd ?? versionNotice?.End;
        return new GameNoticeFeed(region, language, notices, calendar, name, number, versionStart, versionEnd, fetchedAtUtc);
    }

    private static bool IsChinese(string language) => language.StartsWith("zh", StringComparison.OrdinalIgnoreCase);

    private static GameNotice Build(RawNotice item, RawNotice? source, string language, Dictionary<string, DateTimeOffset> versionStarts)
    {
        string zhTitle = source?.Title ?? (IsChinese(language) ? item.Title : string.Empty);
        (NoticeKind kind, GachaPoolType pool) = Classify(item.Category, item.Tag, item.Permanent, zhTitle);

        DateTimeOffset start = item.Start, shelfEnd = item.End;
        DateTimeOffset? end = item.End;
        bool fromContent = false;
        string? timeText = null, endText = null;
        if (source is not null)
        {
            ParsedTime parsed = ParseTime(NoticeHtml.ToPlainText(source.Content), item.Start, item.End, versionStarts);
            start = parsed.Start;
            end = parsed.End;
            fromContent = parsed.FromContent;
            timeText = parsed.Text;
            endText = parsed.EndText;
        }

        string plain = NoticeHtml.ToPlainText(item.Content);
        IReadOnlyList<string> featured = kind == NoticeKind.Gacha ? ExtractFeatured(plain) : [];
        string? poolName = kind == NoticeKind.Gacha && PoolNameRegex().Match(item.Title) is { Success: true } m ? m.Groups[1].Value.Trim() : null;

        return new GameNotice(item.Id, CleanTitle(item.Title), kind, pool, item.Category, item.Tag,
            item.Start, shelfEnd, start, end, fromContent, timeText, endText, featured, poolName,
            item.Banner, item.Content, item.Updated);
    }

    /// <summary>标题里的换行（游戏内分两行显示）替换为空格，并合并多余空白。</summary>
    public static string CleanTitle(string title) =>
        string.Join(' ', title.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    /// <summary>按 category / tag 与简体中文标题归类；没有中文标题时只按 category / tag。</summary>
    public static (NoticeKind Kind, GachaPoolType Pool) Classify(int category, int tag, bool permanent, string zhTitle)
    {
        string title = CleanTitle(zhTitle);
        if (permanent) return (NoticeKind.Info, GachaPoolType.None);
        if (category == 3 || (category != 1 && title.Contains("唤取", StringComparison.Ordinal)))
        {
            bool weapon = title.Contains("武器", StringComparison.Ordinal);
            bool reverb = title.Contains("忆旅", StringComparison.Ordinal);
            GachaPoolType pool = (weapon, reverb) switch
            {
                (true, true) => GachaPoolType.WeaponReverb,
                (true, false) => GachaPoolType.Weapon,
                (false, true) => GachaPoolType.CharacterReverb,
                _ => GachaPoolType.Character,
            };
            return (NoticeKind.Gacha, pool);
        }
        if (InfoTitleWords.Any(w => title.Contains(w, StringComparison.Ordinal))) return (NoticeKind.Info, GachaPoolType.None);
        return category switch
        {
            1 when UpdateTitleWords.Any(w => title.Contains(w, StringComparison.Ordinal)) => (NoticeKind.Update, GachaPoolType.None),
            2 when tag == 4 || ShopTitleWords.Any(w => title.Contains(w, StringComparison.Ordinal)) => (NoticeKind.Shop, GachaPoolType.None),
            2 => (NoticeKind.Event, GachaPoolType.None),
            _ => (NoticeKind.Info, GachaPoolType.None),
        };
    }

    private sealed record ParsedTime(DateTimeOffset Start, DateTimeOffset? End, bool FromContent, string? Text, string? EndText);

    /// <summary>
    /// 从正文（中文纯文本）里找「✦活动时间✦」等小节，解析其中的时间段。
    /// 端点可以是完整日期、只有月日（年份按上架时间推断）或「x.x版本更新后」（取该版本更新维护的结束时间）。
    /// 解析不到或结果不合理时退回上下架时间。
    /// </summary>
    private static ParsedTime ParseTime(string text, DateTimeOffset shelfStart, DateTimeOffset shelfEnd, Dictionary<string, DateTimeOffset> versionStarts)
    {
        var fallback = new ParsedTime(shelfStart, shelfEnd, false, null, null);
        string section = TimeSection(text);
        if (section.Length == 0) return fallback;
        string? line = section.Split('\n').Select(l => l.Trim()).FirstOrDefault(l => l.Any(char.IsDigit) || l.Contains("版本", StringComparison.Ordinal));

        var endpoints = new List<(int Index, int Length, DateTimeOffset? Value, bool Relative)>();
        foreach (Match m in EndpointRegex().Matches(section))
        {
            if (m.Groups["mo"].Success)
                endpoints.Add((m.Index, m.Length, ToDate(m, shelfStart), false));
            else
                endpoints.Add((m.Index, m.Length, VersionStart(m.Groups["v"].Success ? m.Groups["v"].Value : null, versionStarts) ?? shelfStart, true));
        }

        // 「周期……持续时间为3.8版本 ~ 3.9版本」：没有具体结束时间。
        Match versionRange = VersionRangeRegex().Match(section);

        if (endpoints.Count == 0)
        {
            if (versionRange.Success)
                return new ParsedTime(shelfStart, null, true, line, $"{versionRange.Groups["v"].Value} ~ {versionRange.Groups["v2"].Value}");
            return fallback;
        }

        (int index, int length, DateTimeOffset? startValue, _) = endpoints[0];
        DateTimeOffset? endValue = null;
        if (endpoints.Count > 1)
        {
            string between = section[(index + length)..endpoints[1].Index];
            if (Regex.IsMatch(between, "^" + SeparatorPattern + "$")) endValue = endpoints[1].Value;
        }
        else
        {
            // 只有「~ 结束时间」：开始时间不写。
            string before = section[..index].TrimEnd();
            if (before.Length > 0 && "~～-至到—–".Contains(before[^1]))
            {
                endValue = startValue;
                startValue = shelfStart;
            }
        }
        if (startValue is not { } start) return fallback;

        if (endValue is null)
        {
            string rest = section[(index + length)..];
            if (versionRange.Success)
                return new ParsedTime(start, null, true, line, $"{versionRange.Groups["v"].Value} ~ {versionRange.Groups["v2"].Value}");
            // 「……后永久开放」「常驻」：没有结束时间。
            if (Regex.IsMatch(rest, "永久|常驻")) return new ParsedTime(start, null, true, line, null);
            endValue = shelfEnd;
        }
        if (endValue is not { } end || end <= start) return fallback;
        return new ParsedTime(start, end, true, line, null);
    }

    private static string TimeSection(string text)
    {
        int best = -1;
        string? label = null;
        foreach (string candidate in TimeLabels)
        {
            int i = text.IndexOf(candidate, StringComparison.Ordinal);
            if (i >= 0 && (best < 0 || i < best))
            {
                best = i;
                label = candidate;
            }
        }
        if (best < 0) return string.Empty;
        int from = best + label!.Length;
        while (from < text.Length && (text[from] is '✦' or '】' or '：' or ':' || char.IsWhiteSpace(text[from]))) from++;
        int limit = Math.Min(text.Length, from + 260);
        int end = limit;
        foreach (char boundary in new[] { '✦', '【' })
        {
            int b = text.IndexOf(boundary, from, limit - from);
            if (b >= 0) end = Math.Min(end, b);
        }
        // 下一个小节标题（不带装饰的写法）。
        foreach (string word in new[] { "参与条件", "活动介绍", "活动奖励", "活动说明", "活动规则", "开放条件", "解锁条件", "注意事项", "奖励内容" })
        {
            int b = text.IndexOf(word, from, end - from, StringComparison.Ordinal);
            if (b >= 0) end = Math.Min(end, b);
        }
        return text[from..end];
    }

    private static DateTimeOffset? ToDate(Match m, DateTimeOffset reference)
    {
        int month = int.Parse(m.Groups["mo"].Value, CultureInfo.InvariantCulture);
        int day = int.Parse(m.Groups["d"].Value, CultureInfo.InvariantCulture);
        int hour = int.Parse(m.Groups["h"].Value, CultureInfo.InvariantCulture);
        int minute = int.Parse(m.Groups["mi"].Value, CultureInfo.InvariantCulture);
        int second = m.Groups["s"].Success ? int.Parse(m.Groups["s"].Value, CultureInfo.InvariantCulture) : 0;
        if (month is < 1 or > 12 || day is < 1 or > 31 || hour > 24 || minute > 59 || second > 59) return null;
        int year = m.Groups["y"].Success
            ? int.Parse(m.Groups["y"].Value, CultureInfo.InvariantCulture)
            : reference.ToOffset(SourceOffset).Year;
        DateTimeOffset? Make(int y)
        {
            if (day > DateTime.DaysInMonth(y, month)) return null;
            var local = new DateTime(y, month, day, 0, 0, 0).AddHours(hour).AddMinutes(minute).AddSeconds(second);
            return new DateTimeOffset(local, SourceOffset);
        }
        DateTimeOffset? value = Make(year);
        // 没写年份、算出来比上架早一个多月：跨年。
        if (!m.Groups["y"].Success && value is { } v && v < reference.AddDays(-31)) value = Make(year + 1);
        return value;
    }

    private static DateTimeOffset? VersionStart(string? version, Dictionary<string, DateTimeOffset> starts)
    {
        if (version is not null) return starts.TryGetValue(version, out DateTimeOffset value) ? value : null;
        return starts.Count == 1 ? starts.Values.First() : null;
    }

    /// <summary>各版本更新维护的结束时间（取自版本内容说明 /更新说明正文的「更新维护时间」）。</summary>
    private static Dictionary<string, DateTimeOffset> VersionStarts(List<RawNotice> zh)
    {
        var result = new Dictionary<string, DateTimeOffset>(StringComparer.Ordinal);
        foreach (RawNotice notice in zh.OrderBy(n => n.Start))
        {
            if (notice.Category != 1 || VersionNumberRegex().Match(notice.Title) is not { Success: true } vm) continue;
            string text = NoticeHtml.ToPlainText(notice.Content);
            foreach (string label in MaintenanceLabels)
            {
                int i = text.IndexOf(label, StringComparison.Ordinal);
                if (i < 0) continue;
                string section = text.Substring(i, Math.Min(220, text.Length - i));
                MatchCollection dates = DateRegex().Matches(section);
                if (dates.Count < 2) continue;
                if (ToDate(dates[1], notice.Start) is { } end)
                {
                    result[vm.Groups[1].Value] = end;
                    break;
                }
            }
        }
        return result;
    }

    /// <summary>
    /// 当前版本：标题含「版本内容说明」等的更新公告里，进行中的取最新开始的一条，否则取即将开始的，再否则取最近结束的。
    /// 版本名取展示语言标题里引号中的部分（取不到时用中文标题）。
    /// </summary>
    private static (string? Name, string? Number, RawNotice? Notice) CurrentVersion(List<RawNotice> zh, List<RawNotice> shown, DateTimeOffset now)
    {
        var candidates = zh.Where(n => n.Category == 1 && n.Title.Contains("版本", StringComparison.Ordinal)
                                       && n.Title.Contains("内容说明", StringComparison.Ordinal)).ToList();
        if (candidates.Count == 0)
            candidates = zh.Where(n => n.Category == 1 && VersionNumberRegex().IsMatch(n.Title) && QuotedRegex().IsMatch(n.Title)).ToList();
        RawNotice? pick = candidates.Where(n => n.Start <= now && now < n.End).OrderByDescending(n => n.Start).FirstOrDefault()
                          ?? candidates.Where(n => n.Start > now).OrderBy(n => n.Start).FirstOrDefault()
                          ?? candidates.OrderByDescending(n => n.End).FirstOrDefault();
        if (pick is null) return (null, null, null);
        string? number = VersionNumberRegex().Match(pick.Title) is { Success: true } vm ? vm.Groups[1].Value : null;
        RawNotice shownPick = shown.FirstOrDefault(n => n.Id == pick.Id) ?? pick;
        // 中文 / 日文标题用引号括版本名；英文为「Patch Notes for Version 3.8: Lilies in a Dying Dream」。
        string? name = QuotedRegex().Match(shownPick.Title) is { Success: true } q ? q.Groups[1].Value.Trim()
            : Regex.Match(CleanTitle(shownPick.Title), @"\d+(?:\.\d+)+\s*[:：]\s*(.+)$") is { Success: true } en ? en.Groups[1].Value.Trim()
            : QuotedRegex().Match(pick.Title) is { Success: true } zq ? zq.Groups[1].Value.Trim() : null;
        return (name, number, pick);
    }

    /// <summary>
    /// 从正文开头提取 5 星 UP 名字：中文「5星角色「莉莉」」、日文「星5共鳴者「リリー」」、
    /// 英文「5-Star Resonator: Lily, 4-Star …」。都取不到时返回空。
    /// </summary>
    public static IReadOnlyList<string> ExtractFeatured(string plainText)
    {
        string head = plainText.Length > 600 ? plainText[..600] : plainText;
        Match cjk = Regex.Match(head, @"(?:(?<!\d)5\s*星|星\s*5)\s*(?:限定)?\s*(?:角色|武器|共鳴者|共鸣者|武器)\s*((?:[「『][^」』]+[」』][、，,\s]*)+)");
        if (cjk.Success)
        {
            return Regex.Matches(cjk.Groups[1].Value, @"[「『]([^」』]+)[」』]").Select(x => x.Groups[1].Value.Trim())
                .Where(x => x.Length > 0).Distinct().ToList();
        }
        Match en = Regex.Match(head, @"5-Star\s+(?:Resonators?|Weapons?)\s*:\s*(.+?)(?:,?\s*(?:and\s+)?4-Star|\s+receive|[!.。\n])", RegexOptions.IgnoreCase);
        if (en.Success)
        {
            return Regex.Split(en.Groups[1].Value, @"\s*,\s*(?:and\s+)?|\s+and\s+")
                .Select(x => x.Trim()).Where(x => x.Length > 0).Distinct().ToList();
        }
        return [];
    }

    private static NoticeCalendar? ReadCalendar(JsonElement root, List<GameNotice> notices)
    {
        if (!root.TryGetProperty("calendar", out JsonElement list) || list.ValueKind != JsonValueKind.Array) return null;
        JsonElement? chosen = null;
        foreach (JsonElement item in list.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.Object) { chosen = item; break; }
        }
        if (chosen is not { } cal) return null;
        var known = notices.Select(n => n.Id).ToHashSet(StringComparer.Ordinal);
        var nodes = new List<NoticeCalendarNode>();
        if (cal.TryGetProperty("nodes", out JsonElement nodeList) && nodeList.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement node in nodeList.EnumerateArray())
            {
                string id = Str(node, "relatedNoticeId") ?? string.Empty;
                if (id.Length == 0) continue;
                double x1 = Num(node, "x1"), y1 = Num(node, "y1"), x2 = Num(node, "x2"), y2 = Num(node, "y2");
                if (!(x2 > x1 && y2 > y1)) continue;
                nodes.Add(new NoticeCalendarNode(id, Long(node, "relatedNoticeStatus") == 1 && known.Contains(id), x1, y1, x2, y2));
            }
        }
        DateTimeOffset? start = null, end = null;
        if (cal.TryGetProperty("timeline", out JsonElement timeline) && timeline.ValueKind == JsonValueKind.Object)
        {
            start = SourceDate(Str(timeline, "startDate"));
            end = SourceDate(Str(timeline, "endDate"));
        }
        start ??= Long(cal, "startTimeMs") is > 0 and var s ? DateTimeOffset.FromUnixTimeMilliseconds(s) : null;
        end ??= Long(cal, "endTimeMs") is > 0 and var e ? DateTimeOffset.FromUnixTimeMilliseconds(e) : null;
        string? background = Str(cal, "backgroundUrl") is { } url && Uri.TryCreate(url, UriKind.Absolute, out Uri? uri)
                             && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp) ? uri.AbsoluteUri : null;
        return new NoticeCalendar(Str(cal, "id") ?? string.Empty, background, start, end, nodes);
    }

    private static DateTimeOffset? SourceDate(string? text) =>
        DateTime.TryParseExact(text, "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime local)
            ? new DateTimeOffset(local, SourceOffset)
            : null;

    private sealed record RawNotice(string Id, string Title, int Category, int Tag, bool Permanent,
        DateTimeOffset Start, DateTimeOffset End, DateTimeOffset Updated, string? Banner, string Content);

    private static List<RawNotice> ReadNotices(JsonElement root)
    {
        var result = new List<RawNotice>();
        if (root.ValueKind != JsonValueKind.Object) throw new JsonException("公告数据不是对象");
        foreach (string section in new[] { "game", "activity", "recommend" })
        {
            if (!root.TryGetProperty(section, out JsonElement list) || list.ValueKind != JsonValueKind.Array) continue;
            foreach (JsonElement item in list.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object) continue;
                string? id = Str(item, "id");
                string? title = Str(item, "tabTitle");
                long start = Long(item, "startTimeMs"), end = Long(item, "endTimeMs");
                if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(title) || start <= 0 || end <= start) continue;
                string? banner = Url(Str(item, "tabBanner")) ?? Url(Str(item, "foldBanner"));
                long updated = Long(item, "operateTimeMs");
                result.Add(new RawNotice(id.Trim(), title.Trim(), (int)Long(item, "category"), (int)Long(item, "tag"), Long(item, "permanent") == 1,
                    DateTimeOffset.FromUnixTimeMilliseconds(start), DateTimeOffset.FromUnixTimeMilliseconds(end),
                    updated > 0 ? DateTimeOffset.FromUnixTimeMilliseconds(updated) : DateTimeOffset.FromUnixTimeMilliseconds(start),
                    banner, Str(item, "content") ?? string.Empty));
            }
        }
        return result;
    }

    private static string? Url(string? text) =>
        Uri.TryCreate(text?.Trim(), UriKind.Absolute, out Uri? uri) && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp) ? uri.AbsoluteUri : null;

    private static string? Str(JsonElement e, string name)
    {
        if (!e.TryGetProperty(name, out JsonElement v)) return null;
        return v.ValueKind switch
        {
            JsonValueKind.String => v.GetString(),
            JsonValueKind.Number => v.GetRawText(),
            _ => null,
        };
    }

    private static long Long(JsonElement e, string name)
    {
        if (!e.TryGetProperty(name, out JsonElement v)) return 0;
        if (v.ValueKind == JsonValueKind.Number && v.TryGetInt64(out long n)) return n;
        if (v.ValueKind == JsonValueKind.String && long.TryParse(v.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out long s)) return s;
        if (v.ValueKind == JsonValueKind.True) return 1;
        return 0;
    }

    private static double Num(JsonElement e, string name)
    {
        if (!e.TryGetProperty(name, out JsonElement v)) return double.NaN;
        if (v.ValueKind == JsonValueKind.Number) return v.GetDouble();
        return v.ValueKind == JsonValueKind.String && double.TryParse(v.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out double d) ? d : double.NaN;
    }
}
