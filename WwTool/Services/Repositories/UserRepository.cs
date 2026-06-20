using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using WwTool.Common.Context;
using WwTool.Common.Exceptions;
using WwTool.Common.Models;
using WwTool.Services.Interfaces;

namespace WwTool.Services.Repositories
{
    public class UserRepository : IUserRepository
    {
        private readonly ILoggerService _logger;
        private readonly System.Threading.SemaphoreSlim _writeLock = new System.Threading.SemaphoreSlim(1, 1);

        public UserRepository(ILoggerService logger)
        {
            _logger = logger;
        }

        public async Task<UserAccount?> GetUserAccountAsync(string uid)
        {
            try
            {
                using var db = new AppDbContext();
                return await db.UserAccounts.AsNoTracking().FirstOrDefaultAsync(x => x.Uid == uid);
            }
            catch (Exception ex)
            {
                throw new WwToolDatabaseException($"获取本地账号信息失败(Uid: {uid})", ex);
            }
        }

        public async Task<List<UserAccount>> GetAllUserAccountAsync()
        {
            try
            {
                using var db = new AppDbContext();
                return await db.UserAccounts.ToListAsync();
            }
            catch (Exception ex)
            {
                throw new WwToolDatabaseException("获取本地已存储账号列表失败", ex);
            }
        }

        public async Task DeleteUserAccountAsync(string uid)
        {
            await _writeLock.WaitAsync();
            try
            {
                using var db = new AppDbContext();
                var account = await db.UserAccounts.FirstOrDefaultAsync(x => x.Uid == uid);
                if (account != null)
                {
                    db.UserAccounts.Remove(account);
                    await db.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                throw new WwToolDatabaseException($"删除本地账号及数据失败(Uid: {uid})", ex);
            }
            finally
            {
                _writeLock.Release();
            }
        }

        public async Task SaveOauthCodeAsync(string uid, string oauthCode)
        {
            await _writeLock.WaitAsync();
            try
            {
                using var db = new AppDbContext();
                var account = await db.UserAccounts.FirstOrDefaultAsync(x => x.Uid == uid);
                if (account == null)
                {
                    account = new UserAccount { Uid = uid };
                    db.UserAccounts.Add(account);
                }
                account.EncryptedOauthCode = WwTool.Common.Utils.Crypto.Encrypt(oauthCode);
                await db.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                throw new WwToolDatabaseException($"本地保存 OauthCode 失败(Uid: {uid})", ex);
            }
            finally
            {
                _writeLock.Release();
            }
        }

        public async Task<string?> GetOauthCodeAsync(string uid)
        {
            try
            {
                using var db = new AppDbContext();
                var account = await db.UserAccounts.FirstOrDefaultAsync(x => x.Uid == uid);
                if (account == null || string.IsNullOrEmpty(account.EncryptedOauthCode))
                    return null;
                return WwTool.Common.Utils.Crypto.Decrypt(account.EncryptedOauthCode);
            }
            catch
            {
                return null;
            }
        }
    }
}
