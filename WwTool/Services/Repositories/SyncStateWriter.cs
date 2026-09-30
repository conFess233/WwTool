using Microsoft.EntityFrameworkCore;
using WwTool.Common.Context;
using WwTool.Common.Models.Entities;

namespace WwTool.Services.Repositories;

/// <summary>在调用方已有事务中更新同步状态，不创建上下文或自行提交。</summary>
internal static class SyncStateWriter
{
    internal static async Task UpsertAsync(
        AppDbContext db,
        string uid,
        string dataKind,
        string scopeKey,
        DateTimeOffset completedAtUtc,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        SyncState? state = db.SyncStates.Local.FirstOrDefault(
            x => x.Uid == uid && x.DataKind == dataKind && x.ScopeKey == scopeKey);
        state ??= await db.SyncStates.FirstOrDefaultAsync(
            x => x.Uid == uid && x.DataKind == dataKind && x.ScopeKey == scopeKey,
            cancellationToken);
        if (state is null)
        {
            state = new SyncState { Uid = uid, DataKind = dataKind, ScopeKey = scopeKey };
            db.SyncStates.Add(state);
        }
        state.LastSuccessfulSyncAtUtc = completedAtUtc;
    }
}
