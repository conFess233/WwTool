using System.IO;

namespace WwTool.Common.Context;

public sealed record DatabaseOptions(string DatabasePath, string BackupDirectory)
{
    public static DatabaseOptions CreateDefault()
    {
        string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
        return new DatabaseOptions(
            Path.Combine(baseDirectory, "Local", "Data", "LocalData.db"),
            Path.Combine(baseDirectory, "Local", "Backups"));
    }
}
