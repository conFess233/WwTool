namespace WwTool.Core.Abstractions.Platform;

/// <summary>
/// 应用的目录布局。启动时决定一次，其他代码不得自行拼接数据路径。
/// </summary>
public interface IAppPaths
{
    /// <summary>程序所在目录，存放随程序发布的只读资源（语言、图标、内置资料）。</summary>
    string AppDirectory { get; }

    /// <summary>用户数据根目录。便携模式下等于 <see cref="AppDirectory"/>。</summary>
    string DataRoot { get; }

    /// <summary>是否为便携模式（数据写在程序目录）。</summary>
    bool IsPortable { get; }

    /// <summary>配置文件目录：<c>&lt;DataRoot&gt;/Config</c>。</summary>
    string ConfigDirectory { get; }

    /// <summary>本地数据目录：<c>&lt;DataRoot&gt;/Local/Data</c>。</summary>
    string DataDirectory { get; }

    /// <summary>SQLite 数据库文件路径。</summary>
    string DatabasePath { get; }

    /// <summary>数据库备份目录：<c>&lt;DataRoot&gt;/Local/Backups</c>。</summary>
    string BackupDirectory { get; }

    /// <summary>默认日志目录：<c>&lt;DataRoot&gt;/Local/Logs</c>。</summary>
    string LogDirectory { get; }

    /// <summary>可重建的缓存目录：<c>&lt;DataRoot&gt;/Local/Cache</c>。</summary>
    string CacheDirectory { get; }

    /// <summary>随程序发布的只读资源目录：<c>&lt;AppDirectory&gt;/Local</c>。</summary>
    string BundledResourceDirectory { get; }

    /// <summary>
    /// 创建所有可写目录并实际写入测试文件确认可写。
    /// 不可写时抛出 <see cref="UnauthorizedAccessException"/> 或 <see cref="IOException"/>，
    /// 调用方必须停止初始化，不得改用其他目录。
    /// </summary>
    void EnsureWritable();
}
