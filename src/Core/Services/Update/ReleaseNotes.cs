using System.Text;
using System.Text.RegularExpressions;

namespace WwTool.Core.Services.Update;

/// <summary>把发布说明的 Markdown 转成适合直接显示的纯文本：去掉标题井号、粗体星号，链接只留文字，列表符号换成圆点，表格整段省略。</summary>
public static partial class ReleaseNotes
{
    public static string ToPlainText(string? markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown)) return string.Empty;
        var output = new StringBuilder();
        bool blank = false;
        foreach (string raw in markdown.Replace("\r\n", "\n").Split('\n'))
        {
            string line = raw.TrimEnd();
            if (line.TrimStart().StartsWith('|')) continue; // 表格（如下载对照表）在对话框里没法看，省略
            line = Heading().Replace(line, string.Empty);
            line = Bullet().Replace(line, "$1• ");
            line = Image().Replace(line, string.Empty);
            line = Link().Replace(line, "$1");
            line = line.Replace("**", string.Empty).Replace("__", string.Empty).Replace("`", string.Empty);
            if (line.Trim().Length == 0)
            {
                // 连续空行只留一个。
                if (!blank && output.Length > 0) output.Append('\n');
                blank = true;
                continue;
            }
            blank = false;
            output.Append(line).Append('\n');
        }
        return output.ToString().Trim();
    }

    [GeneratedRegex(@"^\s{0,3}#{1,6}\s*")]
    private static partial Regex Heading();

    [GeneratedRegex(@"^(\s*)[-*+]\s+")]
    private static partial Regex Bullet();

    [GeneratedRegex(@"!\[[^\]]*\]\([^)]*\)")]
    private static partial Regex Image();

    [GeneratedRegex(@"\[([^\]]+)\]\([^)]*\)")]
    private static partial Regex Link();
}
