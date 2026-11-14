using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using WwTool.Core.Data.Repositories;
using WwTool.Core.Models.Domain;
using WwTool.Web.Data;
using WwTool.Web.Uids;

namespace WwTool.Web.Auth;

/// <summary>当前站点账号：概览、改名、转移通知、导出与删除。</summary>
public static class MeEndpoints
{
    public sealed record RenameRequest(string? DisplayName);

    public static void MapMe(this IEndpointRouteBuilder api)
    {
        var me = api.MapGroup("/me").RequireAuthorization();

        me.MapGet("/", async (HttpContext http, WebDbContext db, IUserRepository users, CancellationToken token) =>
        {
            Guid userId = http.User.UserId()!.Value;
            SiteUser? user = await db.Users.FindAsync([userId], token);
            if (user is null)
            {
                await http.SignOutAsync(AuthEndpoints.Scheme);
                return Results.Unauthorized();
            }
            // SQLite 不能按 DateTimeOffset 排序，取出后在内存中排序（每个账号的数量都很少）
            List<UidBinding> bindings = (await db.UidBindings.Where(b => b.UserId == userId).ToListAsync(token)).OrderBy(b => b.BoundAt).ToList();
            List<string> uidList = bindings.Select(b => b.Uid).ToList();
            List<UidDataSource> sources = await db.DataSources.Where(s => uidList.Contains(s.Uid)).ToListAsync(token);
            var uids = new List<object>();
            foreach (UidBinding b in bindings)
            {
                AccountSummary? account = await users.GetUserAccountAsync(b.Uid, token);
                uids.Add(new
                {
                    b.Uid,
                    b.Region,
                    b.Method,
                    b.BoundAt,
                    b.LastVerifiedAt,
                    name = account?.Name,
                    level = account?.Level,
                    headPhoto = account?.HeadPhoto,
                    gachaUpdatedAt = sources.FirstOrDefault(s => s.Uid == b.Uid && s.Kind == "gacha")?.UpdatedAt,
                    profileUpdatedAt = sources.FirstOrDefault(s => s.Uid == b.Uid && s.Kind == "profile")?.UpdatedAt,
                });
            }
            var notices = (await db.TransferNotices.Where(n => n.UserId == userId).ToListAsync(token))
                .OrderByDescending(n => n.At).Select(n => new { n.Id, n.MaskedUid, n.At }).ToList();
            return Results.Ok(new { user.Id, user.UserName, user.DisplayName, user.CreatedAt, uids, notices });
        });

        me.MapPatch("/", async (RenameRequest body, HttpContext http, WebDbContext db) =>
        {
            SiteUser? user = await db.Users.FindAsync(http.User.UserId()!.Value);
            if (user is null) return Results.Unauthorized();
            string name = (body.DisplayName ?? string.Empty).Trim();
            if (name.Length is 0 or > 64) return AuthEndpoints.Problem("名称长度需在 1–64 个字符之间。");
            user.DisplayName = name;
            await db.SaveChangesAsync();
            return Results.Ok();
        });

        me.MapDelete("/notices", async (HttpContext http, WebDbContext db) =>
        {
            Guid userId = http.User.UserId()!.Value;
            await db.TransferNotices.Where(n => n.UserId == userId).ExecuteDeleteAsync();
            return Results.Ok();
        });

        me.MapGet("/export", async (HttpContext http, UidDataService data, CancellationToken token) =>
        {
            object export = await data.ExportAsync(http.User.UserId()!.Value, token);
            return Results.Json(export, contentType: "application/json", statusCode: 200);
        }).RequireRateLimiting(RateLimits.Export);

        // 删除站点账号与全部数据，不可恢复
        me.MapDelete("/", async (HttpContext http, UidDataService data, CancellationToken token) =>
        {
            await data.DeleteUserAsync(http.User.UserId()!.Value, token);
            await http.SignOutAsync(AuthEndpoints.Scheme);
            return Results.Ok();
        });
    }
}

public static class RateLimits
{
    public const string Gacha = "gacha";
    public const string Profile = "profile";
    public const string Export = "export";
    public const string Auth = "auth";
    public const string Compute = "compute";
}
