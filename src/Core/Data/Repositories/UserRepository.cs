using WwTool.Core.Extensions;
using WwTool.Core.Abstractions.Platform;
using WwTool.Core.Models.Domain;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using WwTool.Core.Data;
using WwTool.Core.Exceptions;
using WwTool.Core.Data.Entities;
using WwTool.Core.Infrastructure;
using WwTool.Core.Abstractions;

namespace WwTool.Core.Data.Repositories;

public sealed class UserRepository(
    IDbContextFactory<AppDbContext> contextFactory,
    IDatabaseWriteCoordinator writeCoordinator,
    ISecretProtector secretProtector,
    ILoggerService logger) : IUserRepository
{
    private static readonly Expression<Func<UserAccount, AccountSummary>> AccountProjection = account => new()
    {
        Uid = account.Uid, Region = account.Region, Name = account.Name, Level = account.Level,
        Sex = account.Sex, HeadPhoto = account.HeadPhoto, LastSyncedAtUtc = account.LastSyncedAtUtc
    };

    public async Task<AccountSummary?> GetUserAccountAsync(string uid, CancellationToken cancellationToken = default)
    {
        try
        {
            await using AppDbContext db = await contextFactory.CreateDbContextAsync(cancellationToken);
            return await db.UserAccounts.AsNoTracking().Where(x => x.Uid == uid).Select(AccountProjection).FirstOrDefaultAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new WwToolDatabaseException($"获取本地账号信息失败(Uid: {MaskUid(uid)})", ex);
        }
    }

    public async Task<IReadOnlyList<AccountSummary>> GetAllUserAccountAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await using AppDbContext db = await contextFactory.CreateDbContextAsync(cancellationToken);
            return await db.UserAccounts.AsNoTracking().OrderBy(x => x.Uid).Select(AccountProjection).ToListAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new WwToolDatabaseException("获取本地已存储账号列表失败", ex);
        }
    }

    public async Task DeleteUserAccountAsync(string uid, CancellationToken cancellationToken = default)
    {
        try
        {
            await writeCoordinator.ExecuteAsync(async (db, token) =>
            {
                UserAccount? account = await db.UserAccounts.FirstOrDefaultAsync(x => x.Uid == uid, token);
                if (account is not null)
                {
                    string? cUid = await db.GuidePlayerSnapshots.Where(x => x.Uid == uid).Select(x => x.CUid).FirstOrDefaultAsync(token);
                    db.UserAccounts.Remove(account);
                    await db.SaveChangesAsync(token);
                    // 同一个 SDK 账号可关联多个 UID，只清理已没有玩家引用的凭据。
                    if (cUid is not null && !await db.GuidePlayerSnapshots.AnyAsync(x => x.CUid == cUid, token))
                        await db.GuideAccountCredentials.Where(x => x.CUid == cUid).ExecuteDeleteAsync(token);
                }
            }, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new WwToolDatabaseException($"删除本地账号及数据失败(Uid: {MaskUid(uid)})", ex);
        }
    }

    public async Task SaveOauthCodeAsync(string uid, string oauthCode, CancellationToken cancellationToken = default)
    {
        try
        {
            await writeCoordinator.ExecuteAsync(async (db, token) =>
            {
                UserAccount? account = await db.UserAccounts.FirstOrDefaultAsync(x => x.Uid == uid, token);
                if (account is null)
                {
                    account = new UserAccount { Uid = uid };
                    db.UserAccounts.Add(account);
                }

                account.EncryptedOauthCode = secretProtector.Protect(oauthCode);
            }, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new WwToolDatabaseException($"本地保存授权凭据失败(Uid: {MaskUid(uid)})", ex);
        }
    }

    public async Task<string?> GetOauthCodeAsync(string uid, CancellationToken cancellationToken = default)
    {
        try
        {
            await using AppDbContext db = await contextFactory.CreateDbContextAsync(cancellationToken);
            string? encrypted = await db.UserAccounts.AsNoTracking()
                .Where(x => x.Uid == uid)
                .Select(x => x.EncryptedOauthCode)
                .FirstOrDefaultAsync(cancellationToken);
            // 解密失败（换了电脑或系统用户、密文损坏）按未登录处理，由调用方提示重新登录。
            return !string.IsNullOrWhiteSpace(encrypted) && secretProtector.TryUnprotect(encrypted, out string oauthCode) ? oauthCode : null;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.Error($"读取账号授权凭据失败(Uid: {MaskUid(uid)})", ex);
            throw new WwToolDatabaseException("无法读取本地授权凭据，请重新登录。", ex);
        }
    }

    private static string MaskUid(string uid) => uid.Length <= 4 ? "****" : $"***{uid[^4..]}";
}
