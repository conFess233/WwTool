using WwTool.Common.Models.Domain;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using WwTool.Common.Context;
using WwTool.Common.Enums;
using WwTool.Common.Exceptions;
using WwTool.Common.Models;
using WwTool.Common.Models.Entities;
using WwTool.Common.Models.ApiResponse;
using WwTool.Extensions;
using WwTool.Services.Interfaces;

namespace WwTool.Services.Repositories;

public sealed class GachaRepository(
    IDbContextFactory<AppDbContext> contextFactory,
    IDatabaseWriteCoordinator writeCoordinator,
    ILoggerService logger) : IGachaRepository
{
    /// <summary>只查询每个物品的最早时间，避免把整份抽卡记录带到 UI。</summary>
    public Task<WwTool.Common.Models.Domain.AcquisitionTimes> ReadAcquisitionTimesAsync(string uid, int[] pools, CancellationToken cancellationToken = default) => Task.Run(async () =>
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var rows = await db.GachaRecords.AsNoTracking().Where(x => x.Uid == uid && pools.Contains(x.PoolType))
            .GroupBy(x => x.ResourceId).Select(g => new { Id = g.Key, Time = g.Min(x => x.Time) }).ToListAsync(cancellationToken);
        var times = new Dictionary<int, DateTime>();
        foreach (var row in rows)
            if (DateTime.TryParse(row.Time, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out var time)) times[row.Id] = time;
        return new WwTool.Common.Models.Domain.AcquisitionTimes(times, rows.Count > 0);
    }, cancellationToken);
    public async Task<IReadOnlyList<GachaPull>> GetAllRecordsByUidAsync(
        string uid,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using AppDbContext db = await contextFactory.CreateDbContextAsync(cancellationToken);
            List<GachaRecord> records = await db.GachaRecords.AsNoTracking()
                .Where(x => x.Uid == uid)
                .OrderBy(x => x.SourceOrder)
                .ToListAsync(cancellationToken);
            return records.Select(x => ToReadModel(x)).ToList();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new WwToolDatabaseException("读取本地抽卡记录失败。", ex);
        }
    }

    public async Task<IReadOnlyList<GachaPull>> GetPoolRecordsByUidAsync(
        string uid,
        int poolType,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using AppDbContext db = await contextFactory.CreateDbContextAsync(cancellationToken);
            List<GachaRecord> records = await db.GachaRecords.AsNoTracking()
                .Where(x => x.Uid == uid && x.PoolType == poolType)
                .OrderBy(x => x.SourceOrder)
                .ToListAsync(cancellationToken);
            return records.Select(x => ToReadModel(x, poolType)).ToList();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new WwToolDatabaseException("读取指定卡池的本地记录失败。", ex);
        }
    }

    public async Task<int> SyncGachaDataAsync(
        string uid,
        int poolType,
        IEnumerable<GachaPull> records,
        string source = "remote",
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(uid);
        ArgumentNullException.ThrowIfNull(records);

        // 必须保持枚举顺序；这里有意不使用 OrderBy、Sort 或无序集合重建序列。
        List<PreparedRecord> prepared = PrepareInSourceOrder(uid, poolType, records, cancellationToken);
        DateTimeOffset startedAtUtc = DateTimeOffset.UtcNow;

        try
        {
            int inserted = await writeCoordinator.ExecuteAsync(async (db, token) =>
            {
                if (!await db.UserAccounts.AnyAsync(x => x.Uid == uid, token))
                {
                    db.UserAccounts.Add(new UserAccount { Uid = uid });
                }

                HashSet<string> existingFingerprints = await db.GachaRecords.AsNoTracking()
                    .Where(x => x.Uid == uid && x.PoolType == poolType)
                    .Select(x => x.StableFingerprint)
                    .ToHashSetAsync(token);
                long nextSourceOrder = (await db.GachaRecords
                    .Where(x => x.Uid == uid && x.PoolType == poolType)
                    .MaxAsync(x => (long?)x.SourceOrder, token) ?? -1) + 1;

                var batch = new GachaImportBatch
                {
                    Uid = uid,
                    PoolType = poolType,
                    Source = source,
                    StartedAtUtc = startedAtUtc,
                    CompletedAtUtc = DateTimeOffset.UtcNow,
                    RecordCount = prepared.Count
                };
                db.GachaImportBatches.Add(batch);

                int accepted = 0;
                foreach (PreparedRecord item in prepared)
                {
                    token.ThrowIfCancellationRequested();
                    if (!existingFingerprints.Add(item.Fingerprint))
                    {
                        continue;
                    }

                    long sourceOrder = nextSourceOrder++;
                    batch.FirstSourceOrder ??= sourceOrder;
                    batch.LastSourceOrder = sourceOrder;
                    db.GachaRecords.Add(new GachaRecord
                    {
                        Uid = uid,
                        ImportBatch = batch,
                        PoolType = poolType,
                        ResourceId = item.Record.ResourceId,
                        NameSnapshot = item.Record.Name,
                        ResourceType = item.Record.ResourceType,
                        QualityLevel = item.Record.QualityLevel,
                        Time = item.Record.Time,
                        SourceOccurredAtUtc = item.OccurredAtUtc,
                        ApiPageIndex = 0,
                        ResponseItemIndex = item.ResponseItemIndex,
                        SourceOrder = sourceOrder,
                        DuplicateOccurrenceIndex = item.DuplicateOccurrenceIndex,
                        StableFingerprint = item.Fingerprint,
                        ImportedAtUtc = DateTimeOffset.UtcNow
                    });
                    accepted++;
                }

                await SyncStateWriter.UpsertAsync(db, uid, "Gacha", poolType.ToString(CultureInfo.InvariantCulture), DateTimeOffset.UtcNow, token);
                return accepted;
            }, cancellationToken);

            logger.Info($"抽卡同步已完整提交，卡池 {poolType}，接收 {prepared.Count} 条，新增 {inserted} 条。");
            return inserted;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new WwToolDatabaseException($"卡池 {poolType} 的抽卡记录未能完整提交，已保留原数据。", ex);
        }
    }

    private static List<PreparedRecord> PrepareInSourceOrder(
        string uid,
        int poolType,
        IEnumerable<GachaPull> records,
        CancellationToken cancellationToken)
    {
        var result = new List<PreparedRecord>();
        var occurrences = new Dictionary<string, int>(StringComparer.Ordinal);
        int responseItemIndex = 0;
        foreach (GachaPull record in records)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (record.ResourceId <= 0 || record.QualityLevel <= 0 || string.IsNullOrWhiteSpace(record.Time))
            {
                throw new InvalidDataException($"抽卡响应第 {responseItemIndex} 条记录缺少必填字段。");
            }

            string normalized = LegacyGachaRepair.ContentKey(uid, poolType, record.Time, record.ResourceId, record.ResourceType, record.QualityLevel);
            occurrences.TryGetValue(normalized, out int occurrenceIndex);
            occurrences[normalized] = occurrenceIndex + 1;
            string fingerprint = LegacyGachaRepair.Fingerprint(normalized, occurrenceIndex);
            DateTimeOffset? occurredAtUtc = DateTimeOffset.TryParse(
                record.Time,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeLocal,
                out DateTimeOffset parsed) ? parsed.ToUniversalTime() : null;
            result.Add(new PreparedRecord(record, responseItemIndex, occurrenceIndex, fingerprint, occurredAtUtc));
            responseItemIndex++;
        }

        return result;
    }

    private static GachaPull ToReadModel(GachaRecord record, int? poolType = null) => new()
    {
        CardPoolType = poolType is null ? string.Empty : ((CardPoolType)poolType.Value).GetDescription(),
        ResourceId = record.ResourceId,
        Name = record.NameSnapshot ?? string.Empty,
        ResourceType = record.ResourceType ?? string.Empty,
        QualityLevel = record.QualityLevel,
        Time = record.Time
    };

    private sealed record PreparedRecord(
        GachaPull Record,
        int ResponseItemIndex,
        int DuplicateOccurrenceIndex,
        string Fingerprint,
        DateTimeOffset? OccurredAtUtc);
}
