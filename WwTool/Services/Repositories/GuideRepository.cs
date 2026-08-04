using Microsoft.EntityFrameworkCore;
using WwTool.Common.Context;
using WwTool.Common.Exceptions;
using WwTool.Common.Models.ApiResponse;
using WwTool.Common.Models.Entities;
using WwTool.Common.Utils;
using WwTool.Services.Interfaces;

namespace WwTool.Services.Repositories;

public sealed class GuideRepository(
    IDbContextFactory<AppDbContext> contextFactory,
    IDatabaseWriteCoordinator writeCoordinator) : IGuideRepository
{
    public async Task SaveCredentialAndPlayersAsync(string cUid, string token, IReadOnlyList<GuidePlayer> players, CancellationToken cancellationToken = default)
    {
        string encrypted = Crypto.Encrypt(token);
        if (string.IsNullOrWhiteSpace(encrypted))
            throw new WwToolDatabaseException("Guide 令牌加密失败。");

        await writeCoordinator.ExecuteAsync(async (db, ct) =>
        {
            GuideAccountCredential? credential = await db.GuideAccountCredentials.FirstOrDefaultAsync(x => x.CUid == cUid, ct);
            if (credential is null)
            {
                credential = new GuideAccountCredential { CUid = cUid };
                db.GuideAccountCredentials.Add(credential);
            }
            credential.EncryptedGuideToken = encrypted;

            HashSet<string> localUids = await db.UserAccounts.Select(x => x.Uid).ToHashSetAsync(ct);
            foreach (GuidePlayer player in players.Where(x => x.PlayerId.HasValue && !string.IsNullOrWhiteSpace(x.ServerId)))
            {
                string uid = player.PlayerId!.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
                if (!localUids.Contains(uid))
                    continue;
                GuidePlayerSnapshot? mapping = await db.GuidePlayerSnapshots.FirstOrDefaultAsync(x => x.Uid == uid, ct);
                if (mapping is null)
                {
                    mapping = new GuidePlayerSnapshot { Uid = uid };
                    db.GuidePlayerSnapshots.Add(mapping);
                }
                mapping.CUid = cUid;
                mapping.ServerId = player.ServerId;
            }
        }, cancellationToken);
    }

    public async Task<GuideCredential?> GetCredentialAsync(string uid, CancellationToken cancellationToken = default)
    {
        await using AppDbContext db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var data = await db.GuidePlayerSnapshots.AsNoTracking()
            .Where(x => x.Uid == uid)
            .Select(x => new { x.CUid, x.ServerId, x.Credential.EncryptedGuideToken })
            .FirstOrDefaultAsync(cancellationToken);
        if (data is null)
            return null;
        string token = Crypto.Decrypt(data.EncryptedGuideToken);
        return string.IsNullOrWhiteSpace(token) ? null : new GuideCredential(data.CUid, token, data.ServerId);
    }

    public Task DeleteCredentialAsync(string cUid, CancellationToken cancellationToken = default) =>
        writeCoordinator.ExecuteAsync(async (db, ct) =>
        {
            GuideAccountCredential? credential = await db.GuideAccountCredentials.FirstOrDefaultAsync(x => x.CUid == cUid, ct);
            if (credential is not null)
                credential.EncryptedGuideToken = string.Empty;
        }, cancellationToken);

    public Task ReplaceSnapshotAsync(string uid, IReadOnlyList<GuideRoleSnapshot> roles, IReadOnlyList<GuideEquippedWeaponSnapshot> weapons, DateTimeOffset syncedAtUtc, CancellationToken cancellationToken = default) =>
        writeCoordinator.ExecuteAsync(async (db, ct) =>
        {
            GuidePlayerSnapshot player = await db.GuidePlayerSnapshots.FirstAsync(x => x.Uid == uid, ct);
            await db.GuideEquippedWeaponSnapshots.Where(x => x.Uid == uid).ExecuteDeleteAsync(ct);
            await db.GuideRoleSnapshots.Where(x => x.Uid == uid).ExecuteDeleteAsync(ct);
            db.GuideRoleSnapshots.AddRange(roles);
            db.GuideEquippedWeaponSnapshots.AddRange(weapons);
            player.LastSyncedAtUtc = syncedAtUtc;
        }, cancellationToken);

    public async Task<GuideSnapshot> LoadSnapshotAsync(string uid, CancellationToken cancellationToken = default)
    {
        await using AppDbContext db = await contextFactory.CreateDbContextAsync(cancellationToken);
        DateTimeOffset? syncedAt = await db.GuidePlayerSnapshots.AsNoTracking()
            .Where(x => x.Uid == uid).Select(x => x.LastSyncedAtUtc).FirstOrDefaultAsync(cancellationToken);
        List<GuideRoleSnapshot> roles = await db.GuideRoleSnapshots.AsNoTracking()
            .Where(x => x.Uid == uid && x.IsAcquired).OrderBy(x => x.SourceOrder).ToListAsync(cancellationToken);
        List<GuideEquippedWeaponSnapshot> weapons = await db.GuideEquippedWeaponSnapshots.AsNoTracking()
            .Where(x => x.Uid == uid).OrderBy(x => x.SourceOrder).ToListAsync(cancellationToken);
        return new GuideSnapshot(syncedAt, roles, weapons);
    }
}
