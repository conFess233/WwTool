using System.Text.RegularExpressions;
using System.IO;
using WwTool.Common.Exceptions;
using WwTool.Common.Utils;
using WwTool.Services.Interfaces;

namespace WwTool.Services;

public sealed partial class GachaLogLocator : IGachaLogLocator
{
    public Task<string> FindLatestQueryUrlAsync(
        string gamePath,
        string relativeLogPath,
        string logFileName,
        string urlMarker,
        CancellationToken cancellationToken = default) => Task.Run(() =>
    {
        cancellationToken.ThrowIfCancellationRequested();
        string root = gamePath.EndsWith("Wuthering Waves Game", StringComparison.OrdinalIgnoreCase)
            ? gamePath
            : Path.Combine(gamePath, "Wuthering Waves Game");
        string logPath = Path.Combine(root, relativeLogPath, logFileName);
        if (!File.Exists(logPath)) throw new FileNotFoundException("未找到游戏日志文件。", logPath);

        string? line = ReadLines.ReadLinesDecrypt(logPath)
            .LastOrDefault(value => value.Contains(urlMarker, StringComparison.Ordinal));
        if (string.IsNullOrWhiteSpace(line)) throw new WwToolException("游戏日志中没有抽卡查询地址。");
        Match match = QueryUrlRegex().Match(line);
        return match.Success ? match.Groups[1].Value : line;
    }, cancellationToken);

    [GeneratedRegex("\\\"url\\\"\\s*:\\s*\\\"(.*?)\\\"")]
    private static partial Regex QueryUrlRegex();
}
