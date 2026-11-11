namespace WwTool.Core.Abstractions;

public interface IGachaLogLocator
{
    Task<string> FindLatestQueryUrlAsync(
        string gamePath,
        string relativeLogPath,
        string logFileName,
        string urlMarker,
        CancellationToken cancellationToken = default);
}
