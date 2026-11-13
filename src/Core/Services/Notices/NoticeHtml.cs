using System.Globalization;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace WwTool.Core.Services.Notices;

/// <summary>正文文字的语义颜色：公告里的固定几种颜色按色相归类，界面按主题取色。</summary>
public enum NoticeTone
{
    Default,

    /// <summary>强调（公告中的金色小标题与重点）。</summary>
    Accent,

    /// <summary>警示（红色）。</summary>
    Danger,

    /// <summary>弱化（灰色注释）。</summary>
    Muted,
}

public enum NoticeAlign
{
    Left,
    Center,
    Right,
}

/// <summary>一段同样式的文字。<see cref="Link"/> 只保留 http / https 地址。</summary>
public sealed record NoticeRun(string Text, bool Bold = false, bool Italic = false, bool Underline = false, bool Strike = false,
    NoticeTone Tone = NoticeTone.Default, string? Link = null);

/// <summary>正文的一个块。</summary>
public abstract record NoticeBlock;

/// <summary>一段文字；<see cref="Bullet"/> 为列表项前缀（「•」或「1.」），没有时为 null。</summary>
public sealed record NoticeParagraph(IReadOnlyList<NoticeRun> Runs, NoticeAlign Align = NoticeAlign.Left, string? Bullet = null) : NoticeBlock
{
    public string PlainText => string.Concat(Runs.Select(r => r.Text));
}

/// <summary>段落之间的空行（公告用 <c>&lt;div&gt;&amp;nbsp;&lt;/div&gt;</c> 分隔小节）。</summary>
public sealed record NoticeSpacer : NoticeBlock;

public sealed record NoticeImage(string Url, double? Width = null, double? Height = null) : NoticeBlock;

/// <summary>表格：行 → 单元格 → 单元格内的段落。</summary>
public sealed record NoticeTable(IReadOnlyList<IReadOnlyList<IReadOnlyList<NoticeParagraph>>> Rows) : NoticeBlock;

/// <summary>可折叠的一节（<c>&lt;details&gt;</c>）。</summary>
public sealed record NoticeDetails(IReadOnlyList<NoticeRun> Summary, IReadOnlyList<NoticeBlock> Body) : NoticeBlock;

/// <summary>
/// 把游戏内公告正文（飞书导出的 HTML，标签有限）转成简单的块模型，供界面渲染。
/// 只认 div / p / span / strong / em / s / u / a / img / table / ul / ol / li / details / summary / br 等，其余标签只取文字；
/// 不执行任何脚本、不加载样式表。
/// </summary>
public static partial class NoticeHtml
{
    private static readonly HashSet<string> BlockTags = new(StringComparer.OrdinalIgnoreCase)
    {
        "div", "p", "h1", "h2", "h3", "h4", "h5", "h6", "section", "article", "blockquote", "pre", "header", "footer", "hr",
    };

    private static readonly HashSet<string> SkipContentTags = new(StringComparer.OrdinalIgnoreCase) { "head", "style", "script", "title", "colgroup" };

    private static readonly HashSet<string> VoidTags = new(StringComparer.OrdinalIgnoreCase) { "br", "img", "hr", "col", "meta", "link", "input", "wbr" };

    [GeneratedRegex(@"<!--.*?-->|<!\[CDATA\[.*?\]\]>|<!(?:[^>]*)>|<(/?)([A-Za-z][A-Za-z0-9-]*)((?:[^>""']|""[^""]*""|'[^']*')*)>|[^<]+|<", RegexOptions.Singleline)]
    private static partial Regex TokenRegex();

    [GeneratedRegex(@"([A-Za-z_:][-A-Za-z0-9_:.]*)\s*(?:=\s*(?:""([^""]*)""|'([^']*)'|([^\s""'>]+)))?")]
    private static partial Regex AttributeRegex();

    [GeneratedRegex(@"[ \t\r\n\f]+")]
    private static partial Regex SpaceRegex();

    /// <summary>解析正文；空或无法识别时返回空列表。</summary>
    public static IReadOnlyList<NoticeBlock> Parse(string? html)
    {
        if (string.IsNullOrWhiteSpace(html)) return [];
        var builder = new Builder();
        var styles = new Stack<(string Tag, Style Style)>();
        styles.Push(("", default));
        int skipDepth = 0;
        string? skipTag = null;

        foreach (Match m in TokenRegex().Matches(html))
        {
            string token = m.Value;
            if (token.StartsWith("<!", StringComparison.Ordinal)) continue;
            if (!m.Groups[2].Success)
            {
                if (skipDepth > 0) continue;
                // 落单的 "<" 当文字处理。
                builder.Text(WebUtility.HtmlDecode(token), styles.Peek().Style);
                continue;
            }

            bool closing = m.Groups[1].Value == "/";
            string tag = m.Groups[2].Value.ToLowerInvariant();
            Dictionary<string, string> attrs = closing ? [] : Attributes(m.Groups[3].Value);
            bool selfClosing = !closing && (VoidTags.Contains(tag) || m.Groups[3].Value.TrimEnd().EndsWith('/'));

            if (skipDepth > 0)
            {
                if (tag == skipTag) skipDepth += closing ? -1 : (selfClosing ? 0 : 1);
                continue;
            }
            if (SkipContentTags.Contains(tag))
            {
                if (!closing && !selfClosing)
                {
                    skipTag = tag;
                    skipDepth = 1;
                }
                continue;
            }

            if (closing)
            {
                CloseStyles(styles, tag);
                switch (tag)
                {
                    case "li": builder.EndListItem(); break;
                    case "ul" or "ol": builder.EndList(); break;
                    case "td" or "th": builder.EndCell(); break;
                    case "tr": builder.EndRow(); break;
                    case "table": builder.EndTable(); break;
                    case "summary": builder.EndSummary(); break;
                    case "details": builder.EndDetails(); break;
                    default:
                        if (BlockTags.Contains(tag)) builder.Break();
                        break;
                }
                continue;
            }

            Style current = styles.Peek().Style;
            switch (tag)
            {
                case "br":
                    builder.Break(lineBreak: true);
                    continue;
                case "img":
                    if (attrs.TryGetValue("src", out string? src) && SafeUrl(src) is { } url)
                        builder.Image(new NoticeImage(url, Number(attrs, "width"), Number(attrs, "height")));
                    continue;
                case "ul" or "ol":
                    builder.BeginList(tag == "ol", attrs.TryGetValue("start", out string? start) && int.TryParse(start, out int s) ? s : 1);
                    break;
                case "li":
                    builder.BeginListItem();
                    break;
                case "table":
                    builder.BeginTable();
                    break;
                case "tr":
                    builder.BeginRow();
                    break;
                case "td" or "th":
                    builder.BeginCell();
                    break;
                case "details":
                    builder.BeginDetails();
                    break;
                case "summary":
                    builder.BeginSummary();
                    break;
                default:
                    if (BlockTags.Contains(tag)) builder.Break(align: AlignOf(attrs));
                    break;
            }
            if (selfClosing) continue;

            Style next = current;
            switch (tag)
            {
                case "strong" or "b" or "th": next = next with { Bold = true }; break;
                case "em" or "i": next = next with { Italic = true }; break;
                case "u" or "ins": next = next with { Underline = true }; break;
                case "s" or "del" or "strike": next = next with { Strike = true }; break;
                case "a":
                    if (attrs.TryGetValue("href", out string? href) && SafeUrl(href) is { } link) next = next with { Link = link };
                    break;
                case var h when h.Length == 2 && h[0] == 'h' && char.IsDigit(h[1]): next = next with { Bold = true }; break;
            }
            if (attrs.TryGetValue("style", out string? style))
            {
                if (ToneOf(style) is { } tone) next = next with { Tone = tone };
                if (Regex.IsMatch(style, @"font-weight\s*:\s*(bold|[6-9]00)", RegexOptions.IgnoreCase)) next = next with { Bold = true };
                if (Regex.IsMatch(style, @"text-decoration[^;]*line-through", RegexOptions.IgnoreCase)) next = next with { Strike = true };
                if (Regex.IsMatch(style, @"text-decoration[^;]*underline", RegexOptions.IgnoreCase)) next = next with { Underline = true };
            }
            styles.Push((tag, next));
        }
        return builder.Finish();
    }

    /// <summary>正文的纯文本（块之间换行），用于时间等关键字解析。</summary>
    public static string ToPlainText(string? html) => ToPlainText(Parse(html));

    public static string ToPlainText(IReadOnlyList<NoticeBlock> blocks)
    {
        var sb = new StringBuilder();
        void Walk(IEnumerable<NoticeBlock> items)
        {
            foreach (NoticeBlock block in items)
            {
                switch (block)
                {
                    case NoticeParagraph p:
                        if (p.Bullet is not null) sb.Append(p.Bullet).Append(' ');
                        sb.Append(p.PlainText).Append('\n');
                        break;
                    case NoticeSpacer:
                        sb.Append('\n');
                        break;
                    case NoticeTable t:
                        foreach (var row in t.Rows)
                            sb.Append(string.Join(" | ", row.Select(cell => string.Join(" ", cell.Select(c => c.PlainText))))).Append('\n');
                        break;
                    case NoticeDetails d:
                        sb.Append(string.Concat(d.Summary.Select(r => r.Text))).Append('\n');
                        Walk(d.Body);
                        break;
                }
            }
        }
        Walk(blocks);
        return sb.ToString();
    }

    /// <summary>正文里引用的图片地址（缓存清理用）。</summary>
    public static IEnumerable<string> ImageUrls(IReadOnlyList<NoticeBlock> blocks)
    {
        foreach (NoticeBlock block in blocks)
        {
            if (block is NoticeImage image) yield return image.Url;
            else if (block is NoticeDetails details)
            {
                foreach (string url in ImageUrls(details.Body)) yield return url;
            }
        }
    }

    /// <summary>
    /// 按色相把内联颜色归类：低饱和的深色视为默认、浅灰为弱化；偏红为警示；其余有颜色的为强调。
    /// 公告只用金色、红色、灰色和近黑色几种。
    /// </summary>
    public static NoticeTone? ToneOf(string style)
    {
        Match m = Regex.Match(style, @"(?<![-\w])color\s*:\s*([^;]+)", RegexOptions.IgnoreCase);
        if (!m.Success || ParseColor(m.Groups[1].Value.Trim()) is not { } rgb) return null;
        double r = rgb.R / 255.0, g = rgb.G / 255.0, b = rgb.B / 255.0;
        double max = Math.Max(r, Math.Max(g, b)), min = Math.Min(r, Math.Min(g, b));
        double lightness = (max + min) / 2;
        double saturation = max == min ? 0 : (max - min) / (1 - Math.Abs(2 * lightness - 1));
        if (saturation < 0.18)
        {
            if (lightness < 0.35) return NoticeTone.Default;
            return lightness > 0.92 ? NoticeTone.Default : NoticeTone.Muted;
        }
        double hue;
        if (max == r) hue = 60 * (((g - b) / (max - min)) % 6);
        else if (max == g) hue = 60 * ((b - r) / (max - min) + 2);
        else hue = 60 * ((r - g) / (max - min) + 4);
        if (hue < 0) hue += 360;
        return hue < 20 || hue >= 330 ? NoticeTone.Danger : NoticeTone.Accent;
    }

    private static (int R, int G, int B)? ParseColor(string value)
    {
        Match rgb = Regex.Match(value, @"rgba?\(\s*(\d+)\s*,\s*(\d+)\s*,\s*(\d+)", RegexOptions.IgnoreCase);
        if (rgb.Success)
            return (Clamp(rgb.Groups[1].Value), Clamp(rgb.Groups[2].Value), Clamp(rgb.Groups[3].Value));
        Match hex = Regex.Match(value, @"^#([0-9a-f]{3}|[0-9a-f]{6})\b", RegexOptions.IgnoreCase);
        if (!hex.Success) return null;
        string h = hex.Groups[1].Value;
        if (h.Length == 3) h = string.Concat(h.Select(c => new string(c, 2)));
        return (Convert.ToInt32(h[..2], 16), Convert.ToInt32(h[2..4], 16), Convert.ToInt32(h[4..], 16));
    }

    private static int Clamp(string text) => Math.Clamp(int.Parse(text, CultureInfo.InvariantCulture), 0, 255);

    private static NoticeAlign AlignOf(Dictionary<string, string> attrs)
    {
        string text = (attrs.GetValueOrDefault("style") ?? string.Empty) + ";" + (attrs.GetValueOrDefault("align") is { } a ? "text-align:" + a : "");
        Match m = Regex.Match(text, @"text-align\s*:\s*(center|right)", RegexOptions.IgnoreCase);
        if (!m.Success) return NoticeAlign.Left;
        return m.Groups[1].Value.Equals("center", StringComparison.OrdinalIgnoreCase) ? NoticeAlign.Center : NoticeAlign.Right;
    }

    private static double? Number(Dictionary<string, string> attrs, string name) =>
        attrs.TryGetValue(name, out string? text) && double.TryParse(text.Replace("px", "", StringComparison.OrdinalIgnoreCase),
            NumberStyles.Float, CultureInfo.InvariantCulture, out double value) && value > 0 ? value : null;

    private static string? SafeUrl(string? raw)
    {
        string url = WebUtility.HtmlDecode(raw ?? string.Empty).Trim();
        if (url.StartsWith("//", StringComparison.Ordinal)) url = "https:" + url;
        return Uri.TryCreate(url, UriKind.Absolute, out Uri? uri) && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp)
            ? uri.AbsoluteUri
            : null;
    }

    private static Dictionary<string, string> Attributes(string text)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match m in AttributeRegex().Matches(text))
        {
            string value = m.Groups[2].Success ? m.Groups[2].Value : m.Groups[3].Success ? m.Groups[3].Value : m.Groups[4].Value;
            result.TryAdd(m.Groups[1].Value, value);
        }
        return result;
    }

    /// <summary>关闭标签时弹出到同名的样式层；找不到同名层（标签不配对）时不动。</summary>
    private static void CloseStyles(Stack<(string Tag, Style Style)> styles, string tag)
    {
        if (!styles.Any(s => s.Tag == tag)) return;
        while (styles.Count > 1)
        {
            (string top, _) = styles.Pop();
            if (top == tag) break;
        }
    }

    private readonly record struct Style(bool Bold, bool Italic, bool Underline, bool Strike, NoticeTone Tone, string? Link);

    /// <summary>按遇到的标签逐步拼出块；表格、列表、折叠块各自有一层容器。</summary>
    private sealed class Builder
    {
        private sealed class Container
        {
            public List<NoticeBlock> Blocks { get; } = [];
        }

        private sealed class ListState(bool ordered, int next)
        {
            public bool Ordered { get; } = ordered;
            public int Next { get; set; } = next;
        }

        private sealed class TableState
        {
            public List<IReadOnlyList<IReadOnlyList<NoticeParagraph>>> Rows { get; } = [];
            public List<IReadOnlyList<NoticeParagraph>>? Row { get; set; }
            public Container? Cell { get; set; }
            public Stack<Container> Saved { get; } = new();
        }

        private sealed class DetailsState
        {
            public List<NoticeRun> Summary { get; } = [];
            public Container Body { get; } = new();
            public bool InSummary { get; set; }
        }

        private readonly Container _root = new();
        private readonly Stack<Container> _containers = new();
        private readonly Stack<ListState> _lists = new();
        private readonly Stack<TableState> _tables = new();
        private readonly Stack<DetailsState> _details = new();
        private readonly List<NoticeRun> _runs = [];
        private NoticeAlign _align;
        private string? _bullet;
        private bool _lastWasSpace = true;

        public Builder() => _containers.Push(_root);

        private Container Current => _containers.Peek();

        public void Text(string text, Style style)
        {
            // HTML 空白折叠；&nbsp; 解码后是 U+00A0，作为普通空格保留（段落只有它时视为空行）。
            string collapsed = SpaceRegex().Replace(text, " ");
            if (collapsed.Length == 0) return;
            if (_lastWasSpace && collapsed[0] == ' ') collapsed = collapsed[1..];
            if (collapsed.Length == 0) return;
            _lastWasSpace = collapsed[^1] == ' ';
            if (_details.TryPeek(out DetailsState? d) && d.InSummary)
            {
                d.Summary.Add(Run(collapsed, style));
                return;
            }
            NoticeRun run = Run(collapsed, style);
            if (_runs.Count > 0 && SameStyle(_runs[^1], run)) _runs[^1] = _runs[^1] with { Text = _runs[^1].Text + run.Text };
            else _runs.Add(run);
        }

        private static NoticeRun Run(string text, Style s) => new(text, s.Bold, s.Italic, s.Underline, s.Strike, s.Tone, s.Link);

        private static bool SameStyle(NoticeRun a, NoticeRun b) => a with { Text = "" } == b with { Text = "" };

        /// <summary>结束当前段落（块级标签边界或换行）。</summary>
        public void Break(bool lineBreak = false, NoticeAlign? align = null)
        {
            FlushParagraph(keepEmptyAsSpacer: lineBreak);
            if (align is { } a) _align = a;
        }

        private void FlushParagraph(bool keepEmptyAsSpacer = false)
        {
            var runs = TrimRuns(_runs);
            _runs.Clear();
            _lastWasSpace = true;
            if (runs.Count == 0)
            {
                // 只有空格 / &nbsp; 的段落：小节之间的空行。
                if (keepEmptyAsSpacer || _hadNbsp) AddSpacer();
                _hadNbsp = false;
                _align = NoticeAlign.Left;
                return;
            }
            _hadNbsp = false;
            Current.Blocks.Add(new NoticeParagraph(runs, _align, _bullet));
            _bullet = null;
            _align = NoticeAlign.Left;
        }

        private bool _hadNbsp;

        private List<NoticeRun> TrimRuns(List<NoticeRun> runs)
        {
            var list = runs.Select(r => r).ToList();
            if (list.Count > 0 && list.All(r => string.IsNullOrWhiteSpace(r.Text.Replace(' ', ' '))))
            {
                _hadNbsp = list.Any(r => r.Text.Contains(' '));
                return [];
            }
            for (int i = 0; i < list.Count; i++) list[i] = list[i] with { Text = list[i].Text.Replace(' ', ' ') };
            while (list.Count > 0 && string.IsNullOrWhiteSpace(list[0].Text)) list.RemoveAt(0);
            while (list.Count > 0 && string.IsNullOrWhiteSpace(list[^1].Text)) list.RemoveAt(list.Count - 1);
            if (list.Count == 0) return list;
            list[0] = list[0] with { Text = list[0].Text.TrimStart() };
            list[^1] = list[^1] with { Text = list[^1].Text.TrimEnd() };
            return list;
        }

        private void AddSpacer()
        {
            List<NoticeBlock> blocks = Current.Blocks;
            // 开头与连续的空行只留一个。
            if (blocks.Count == 0 || blocks[^1] is NoticeSpacer) return;
            blocks.Add(new NoticeSpacer());
        }

        public void Image(NoticeImage image)
        {
            FlushParagraph();
            Current.Blocks.Add(image);
        }

        public void BeginList(bool ordered, int start)
        {
            FlushParagraph();
            _lists.Push(new ListState(ordered, start));
        }

        public void EndList()
        {
            FlushParagraph();
            _lists.TryPop(out _);
        }

        public void BeginListItem()
        {
            FlushParagraph();
            if (_lists.TryPeek(out ListState? list))
            {
                _bullet = list.Ordered ? list.Next.ToString(CultureInfo.InvariantCulture) + "." : "•";
                list.Next++;
            }
            else _bullet = "•";
        }

        public void EndListItem() => FlushParagraph();

        public void BeginTable()
        {
            FlushParagraph();
            _tables.Push(new TableState());
        }

        public void BeginRow()
        {
            if (!_tables.TryPeek(out TableState? t)) return;
            EndCell();
            EndRow();
            t.Row = [];
        }

        public void BeginCell()
        {
            if (!_tables.TryPeek(out TableState? t)) return;
            EndCell();
            t.Row ??= [];
            FlushParagraph();
            t.Cell = new Container();
            _containers.Push(t.Cell);
        }

        public void EndCell()
        {
            if (!_tables.TryPeek(out TableState? t) || t.Cell is null) return;
            FlushParagraph();
            if (ReferenceEquals(_containers.Peek(), t.Cell)) _containers.Pop();
            t.Row?.Add(t.Cell.Blocks.OfType<NoticeParagraph>().ToList());
            t.Cell = null;
        }

        public void EndRow()
        {
            if (!_tables.TryPeek(out TableState? t)) return;
            EndCell();
            if (t.Row is { Count: > 0 } row) t.Rows.Add(row);
            t.Row = null;
        }

        public void EndTable()
        {
            if (!_tables.TryPop(out TableState? t)) return;
            _tables.Push(t);
            EndRow();
            _tables.Pop();
            if (t.Rows.Count > 0) Current.Blocks.Add(new NoticeTable(t.Rows));
        }

        public void BeginDetails()
        {
            FlushParagraph();
            var d = new DetailsState();
            _details.Push(d);
            _containers.Push(d.Body);
        }

        public void BeginSummary()
        {
            if (_details.TryPeek(out DetailsState? d))
            {
                FlushParagraph();
                d.InSummary = true;
                _lastWasSpace = true;
            }
        }

        public void EndSummary()
        {
            if (_details.TryPeek(out DetailsState? d)) d.InSummary = false;
            _lastWasSpace = true;
        }

        public void EndDetails()
        {
            if (!_details.TryPop(out DetailsState? d)) return;
            FlushParagraph();
            if (ReferenceEquals(_containers.Peek(), d.Body)) _containers.Pop();
            List<NoticeRun> summary = d.Summary.Count > 0 ? d.Summary : [new NoticeRun("…")];
            summary[0] = summary[0] with { Text = summary[0].Text.TrimStart() };
            summary[^1] = summary[^1] with { Text = summary[^1].Text.TrimEnd() };
            Current.Blocks.Add(new NoticeDetails(summary, TrimSpacers(d.Body.Blocks)));
        }

        public IReadOnlyList<NoticeBlock> Finish()
        {
            while (_details.Count > 0) EndDetails();
            while (_tables.Count > 0) EndTable();
            FlushParagraph();
            while (_containers.Count > 1) _containers.Pop();
            return TrimSpacers(_root.Blocks);
        }

        private static List<NoticeBlock> TrimSpacers(List<NoticeBlock> blocks)
        {
            var list = new List<NoticeBlock>(blocks);
            while (list.Count > 0 && list[0] is NoticeSpacer) list.RemoveAt(0);
            while (list.Count > 0 && list[^1] is NoticeSpacer) list.RemoveAt(list.Count - 1);
            return list;
        }
    }
}
