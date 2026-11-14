using Microsoft.EntityFrameworkCore;
using WwTool.Web.Data;

namespace WwTool.Web.Uids;

/// <summary>可以直接显示给用户的错误（不含完整链接、完整 UID 或凭据）。</summary>
public class UserFacingException(string message) : Exception(message);

/// <summary>
/// UID 与站点账号的绑定。两种方式都可以绑定或把绑定转移到自己名下：
/// 提交该 UID 的有效抽卡链接（后端验证），或在浏览器内登录游戏账号后上报账号资料（信任浏览器上报）。
/// 发生转移时给原账号留下提示。
/// </summary>
public sealed class UidBindingService(IDbContextFactory<WebDbContext> webDb)
{
    /// <summary>绑定 UID；已属于其他站点账号时转移。返回是否发生了转移。</summary>
    public async Task<bool> BindUidAsync(Guid userId, string uid, string region, string method, CancellationToken token)
    {
        await using WebDbContext db = await webDb.CreateDbContextAsync(token);
        DateTimeOffset now = DateTimeOffset.UtcNow;
        UidBinding? binding = await db.UidBindings.FindAsync([uid], token);
        bool transferred = false;
        if (binding is null || binding.UserId != userId)
        {
            if (await db.UidBindings.CountAsync(b => b.UserId == userId, token) >= Limits.MaxUidsPerUser)
                throw new UserFacingException($"每个账号最多绑定 {Limits.MaxUidsPerUser} 个 UID。");
        }
        if (binding is null)
        {
            db.UidBindings.Add(binding = new UidBinding { Uid = uid, UserId = userId, BoundAt = now });
        }
        else if (binding.UserId != userId)
        {
            db.TransferNotices.Add(new UidTransferNotice { Id = Guid.NewGuid(), UserId = binding.UserId, MaskedUid = Mask(uid), At = now });
            binding.UserId = userId;
            binding.BoundAt = now;
            transferred = true;
        }
        binding.Region = region;
        binding.Method = method;
        binding.LastVerifiedAt = now;
        await db.SaveChangesAsync(token);
        return transferred;
    }

    public async Task MarkSourceAsync(string uid, string kind, string source, CancellationToken token)
    {
        await using WebDbContext db = await webDb.CreateDbContextAsync(token);
        UidDataSource? row = await db.DataSources.FindAsync([uid, kind], token);
        if (row is null) db.DataSources.Add(row = new UidDataSource { Uid = uid, Kind = kind });
        row.Source = source;
        row.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(token);
    }

    public static string Mask(string uid) => uid.Length <= 4 ? "****" : uid[..^4] + "****";
}
