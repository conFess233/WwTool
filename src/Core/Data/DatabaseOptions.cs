using WwTool.Core.Abstractions.Platform;

namespace WwTool.Core.Data;

/// <summary>
/// 数据库文件与备份目录。路径一律来自 <see cref="IAppPaths"/>，不在仓储中拼接。
/// </summary>
public sealed record DatabaseOptions(string DatabasePath, string BackupDirectory)
{
    public static DatabaseOptions FromPaths(IAppPaths paths)
    {
        ArgumentNullException.ThrowIfNull(paths);
        return new DatabaseOptions(paths.DatabasePath, paths.BackupDirectory);
    }
}
