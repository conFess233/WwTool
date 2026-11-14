using WwTool.Core.Abstractions.Platform;

namespace WwTool.Web.Platform;

/// <summary>
/// Web 后端的目录布局：数据根目录来自配置 <c>WwTool:DataRoot</c>（相对路径按程序目录解析），
/// 只读资源随程序发布在 <c>&lt;程序目录&gt;/Local</c>。启动时决定一次。
/// </summary>
public sealed class WebAppPaths : IAppPaths
{
    public WebAppPaths(string? dataRoot)
    {
        AppDirectory = AppContext.BaseDirectory;
        DataRoot = Path.GetFullPath(string.IsNullOrWhiteSpace(dataRoot) ? "data" : dataRoot, AppDirectory);
    }

    public string AppDirectory { get; }

    public string DataRoot { get; }

    public bool IsPortable => false;

    public string ConfigDirectory => Path.Combine(DataRoot, "Config");

    public string DataDirectory => Path.Combine(DataRoot, "Local", "Data");

    public string DatabasePath => Path.Combine(DataDirectory, "WebData.db");

    public string BackupDirectory => Path.Combine(DataRoot, "Local", "Backups");

    public string LogDirectory => Path.Combine(DataRoot, "Local", "Logs");

    public string CacheDirectory => Path.Combine(DataRoot, "Local", "Cache");

    public string BundledResourceDirectory => Path.Combine(AppDirectory, "Local");

    public void EnsureWritable()
    {
        foreach (string directory in new[] { ConfigDirectory, DataDirectory, BackupDirectory, LogDirectory, CacheDirectory })
        {
            Directory.CreateDirectory(directory);
            string probe = Path.Combine(directory, $".write-test-{Guid.NewGuid():N}");
            File.WriteAllText(probe, string.Empty);
            File.Delete(probe);
        }
    }
}
