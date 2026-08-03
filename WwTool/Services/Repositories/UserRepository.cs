using Microsoft.EntityFrameworkCore;
using WwTool.Common.Context;
using WwTool.Common.Exceptions;
using WwTool.Common.Models;
using WwTool.Common.Models.Entities;
using WwTool.Common.Utils;
using WwTool.Services.Interfaces;

namespace WwTool.Services.Repositories;

public sealed class UserRepository(
    IDbContextFactory<AppDbContext> contextFactory,
    IDatabaseWriteCoordinator writeCoordinator,
    ILoggerService logger) : IUserRepository
{
    public async Task<UserAccount?> GetUserAccountAsync(string uid, CancellationToken cancellationToken = default)
    {
        try
        {
            await using AppDbContext db = await contextFactory.CreateDbContextAsync(cancellationToken);
            return await db.UserAccounts.AsNoTracking().FirstOrDefaultAsync(x => x.Uid == uid, cancellationToken);
        }
        catch (Exception ex)
        {
            throw new WwToolDatabaseException($"获取本地账号信息失败(Uid: {MaskUid(uid)})", ex);
        }
    }

    public async Task<IReadOnlyList<UserAccount>> GetAllUserAccountAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await using AppDbContext db = await contextFactory.CreateDbContextAsync(cancellationToken);
            return await db.UserAccounts.AsNoTracking().OrderBy(x => x.Uid).ToListAsync(cancellationToken);
        }
        catch (Exception ex)
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
                    db.UserAccounts.Remove(account);
                }
            }, cancellationToken);
        }
        catch (Exception ex)
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

                account.EncryptedOauthCode = Crypto.Encrypt(oauthCode);
            }, cancellationToken);
        }
        catch (Exception ex)
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
            return string.IsNullOrWhiteSpace(encrypted) ? null : Crypto.Decrypt(encrypted);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.Error($"读取账号授权凭据失败(Uid: {MaskUid(uid)})", ex);
            throw new WwToolDatabaseException("无法读取本地授权凭据，请重新登录。", ex);
        }
    }

    private static string MaskUid(string uid) => uid.Length <= 4 ? "****" : $"***{uid[^4..]}";
}
