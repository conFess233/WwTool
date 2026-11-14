using Microsoft.EntityFrameworkCore;
using WwTool.Core.Data.Repositories;
using WwTool.Core.Enums;
using WwTool.Core.Models.Domain;
using WwTool.Web.Data;

namespace WwTool.Web.Uids;

/// <summary>删除与导出：解绑 UID 时一并删除后端保存的该 UID 全部数据。</summary>
public sealed class UidDataService(
    IDbContextFactory<WebDbContext> webDb,
    IUserRepository users,
    IGachaRepository gacha,
    IPlayerInfoRepository players)
{
    public async Task DeleteUidAsync(string uid, CancellationToken token)
    {
        await gacha.DeleteByUidAsync(uid, token);
        await users.DeleteUserAccountAsync(uid, token);
        await using WebDbContext db = await webDb.CreateDbContextAsync(token);
        await db.DataSources.Where(x => x.Uid == uid).ExecuteDeleteAsync(token);
        await db.UidBindings.Where(x => x.Uid == uid).ExecuteDeleteAsync(token);
    }

    /// <summary>删除站点账号：先删每个已绑定 UID 的数据，再删账号（通知随外键级联删除）。</summary>
    public async Task DeleteUserAsync(Guid userId, CancellationToken token)
    {
        List<string> uids;
        await using (WebDbContext db = await webDb.CreateDbContextAsync(token))
            uids = await db.UidBindings.Where(b => b.UserId == userId).Select(b => b.Uid).ToListAsync(token);
        foreach (string uid in uids) await DeleteUidAsync(uid, token);
        await using WebDbContext db2 = await webDb.CreateDbContextAsync(token);
        await db2.Users.Where(u => u.Id == userId).ExecuteDeleteAsync(token);
    }

    /// <summary>导出站点账号下的全部数据（不含任何凭据）。</summary>
    public async Task<object> ExportAsync(Guid userId, CancellationToken token)
    {
        await using WebDbContext db = await webDb.CreateDbContextAsync(token);
        SiteUser user = await db.Users.FirstAsync(u => u.Id == userId, token);
        List<UidBinding> bindings = await db.UidBindings.Where(b => b.UserId == userId).ToListAsync(token);
        var result = new List<object>();
        foreach (UidBinding b in bindings)
        {
            var pools = new Dictionary<int, IReadOnlyList<GachaPull>>();
            foreach (CardPoolType type in Enum.GetValues<CardPoolType>())
                pools[(int)type] = await gacha.GetPoolRecordsByUidAsync(b.Uid, (int)type, token);
            PlayerSnapshot? snapshot = await players.LoadPlayerRoleDataAsync(b.Uid, token);
            result.Add(new
            {
                uid = b.Uid,
                region = b.Region,
                boundAt = b.BoundAt,
                // 每个卡池按存储顺序（SourceOrder）导出，导入时保持原顺序
                gacha = pools.ToDictionary(p => p.Key, p => p.Value.Select(GachaMapper.Record)),
                profile = snapshot is null ? null : ProfileMapper.Map(snapshot),
            });
        }
        return new
        {
            format = "wwtool-web-export",
            version = 1,
            exportedAt = DateTimeOffset.UtcNow,
            account = new { user.DisplayName, user.CreatedAt },
            uids = result,
        };
    }
}
