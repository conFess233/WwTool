using System.IO;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace WwTool.Common.Context;

/// <summary>把旧批次指纹升级为内容及出现次数指纹，保留旧记录顺序。</summary>
public static class LegacyGachaRepair
{
    public static string ContentKey(string uid, int pool, string time, int resource, string? type, int quality) =>
        $"{uid}|{pool}|{time.Trim()}|{resource}|{type?.Trim()}|{quality}";

    public static string Fingerprint(string key, int occurrence) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"v1|{key}|{occurrence}")));

    /// <summary>仅修复带旧迁移标记的记录；先备份，再在一个事务内协调已知重叠。</summary>
    public static async Task RepairAsync(AppDbContext db, CancellationToken token)
    {
        var legacy = await db.GachaRecords.Where(x => x.StableFingerprint.StartsWith("legacy-v1-"))
            .OrderBy(x => x.SourceOrder).ToListAsync(token);
        if (legacy.Count == 0) return;

        var source = (SqliteConnection)db.Database.GetDbConnection();
        await db.Database.OpenConnectionAsync(token);
        string backupDirectory = Path.Combine(Path.GetDirectoryName(source.DataSource)!, "Backups");
        Directory.CreateDirectory(backupDirectory);
        string backupPath = Path.Combine(backupDirectory, $"before-gacha-fingerprint-repair-{DateTime.UtcNow:yyyyMMddHHmmssfff}.db");
        using (var destination = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = backupPath }.ToString()))
        {
            await destination.OpenAsync(token);
            source.BackupDatabase(destination);
        }

        await using var transaction = await db.Database.BeginTransactionAsync(token);
        var occurrences = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var record in legacy)
        {
            token.ThrowIfCancellationRequested();
            string key = ContentKey(record.Uid, record.PoolType, record.Time, record.ResourceId, record.ResourceType, record.QualityLevel);
            occurrences.TryGetValue(key, out int occurrence);
            occurrences[key] = occurrence + 1;
            string fingerprint = Fingerprint(key, occurrence);
            // 一一对应出现序号，只删除由旧/新指纹不兼容形成的重叠，不合并合法重复。
            await db.GachaRecords.Where(x => x.Uid == record.Uid && x.PoolType == record.PoolType && x.StableFingerprint == fingerprint)
                .ExecuteDeleteAsync(token);
            record.StableFingerprint = fingerprint;
            record.DuplicateOccurrenceIndex = occurrence;
        }
        await db.SaveChangesAsync(token);
        await transaction.CommitAsync(token);
    }
}
