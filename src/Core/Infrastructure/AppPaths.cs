using WwTool.Core.Abstractions.Platform;

namespace WwTool.Core.Infrastructure;

/// <summary>
/// <see cref="IAppPaths"/> 的实现。规则（见规范 01 §5.6）：
/// <list type="number">
/// <item>程序目录有 <c>portable</c> 标记文件，或在 Windows 上程序目录已有 1.2.1 的数据库：数据写在程序目录。</item>
/// <item>否则写在系统数据目录下的 <c>WwTool</c>：Windows <c>%LOCALAPPDATA%</c>、Linux <c>$XDG_DATA_HOME</c>
/// （默认 <c>~/.local/share</c>）、macOS <c>~/Library/Application Support</c>。</item>
/// </list>
/// 规则只在构造时决定一次；选定目录不可写时由 <see cref="EnsureWritable"/> 报错，不会改用别处。
/// </summary>
public sealed class AppPaths : IAppPaths
{
    /// <summary>便携模式标记文件名。</summary>
    public const string PortableMarkerFileName = "portable";

    private const string AppFolderName = "WwTool";

    public AppPaths()
        : this(AppContext.BaseDirectory, Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData, Environment.SpecialFolderOption.Create))
    {
    }

    /// <summary>供测试注入目录。</summary>
    public AppPaths(string appDirectory, string systemDataDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(appDirectory);
        AppDirectory = Path.GetFullPath(appDirectory);

        IsPortable = File.Exists(Path.Combine(AppDirectory, PortableMarkerFileName))
            || (OperatingSystem.IsWindows() && File.Exists(Path.Combine(AppDirectory, "Local", "Data", "LocalData.db")));

        if (!IsPortable && string.IsNullOrWhiteSpace(systemDataDirectory))
        {
            throw new InvalidOperationException("无法确定系统数据目录，请在程序目录放置 portable 标记文件以使用便携模式。");
        }

        DataRoot = IsPortable ? AppDirectory : Path.Combine(Path.GetFullPath(systemDataDirectory), AppFolderName);
        string local = Path.Combine(DataRoot, "Local");
        ConfigDirectory = Path.Combine(DataRoot, "Config");
        DataDirectory = Path.Combine(local, "Data");
        DatabasePath = Path.Combine(DataDirectory, "LocalData.db");
        BackupDirectory = Path.Combine(local, "Backups");
        LogDirectory = Path.Combine(local, "Logs");
        CacheDirectory = Path.Combine(local, "Cache");
        BundledResourceDirectory = Path.Combine(AppDirectory, "Local");
    }

    public string AppDirectory { get; }
    public string DataRoot { get; }
    public bool IsPortable { get; }
    public string ConfigDirectory { get; }
    public string DataDirectory { get; }
    public string DatabasePath { get; }
    public string BackupDirectory { get; }
    public string LogDirectory { get; }
    public string CacheDirectory { get; }
    public string BundledResourceDirectory { get; }

    public void EnsureWritable()
    {
        foreach (string directory in new[] { ConfigDirectory, DataDirectory, BackupDirectory, LogDirectory, CacheDirectory })
        {
            Directory.CreateDirectory(directory);
            string probe = Path.Combine(directory, $".write-test-{Guid.NewGuid():N}");
            // 只检查目录属性不可靠（只读介质、ACL、沙盒），必须真的写一次。
            File.WriteAllText(probe, string.Empty);
            File.Delete(probe);
        }
    }
}
