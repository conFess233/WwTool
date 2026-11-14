using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WwTool.Core.Abstractions;
using WwTool.Core.Data.Repositories;
using WwTool.Core.Enums;
using WwTool.Core.Models.Api.Responses;
using WwTool.Core.Models.Domain;
using WwTool.Core.Services;
using WwTool.Core.Services.Gacha;
using WwTool.Web.Auth;
using WwTool.Web.Data;
using WwTool.Web.Roles;

namespace WwTool.Web.Uids;

/// <summary>
/// UID 相关接口。所有 <c>/api/uids/{uid}/*</c> 都要求 UID 已绑定到当前站点账号。
/// 后端不接触游戏凭据：账号资料由浏览器用 OauthCode 直连库洛获取后，把不含凭据的原始响应转交过来。
/// </summary>
public static class UidEndpoints
{
    public sealed record LinkRequest(string Url);

    /// <summary>浏览器本地保存的记录：键为卡池编号，值按本地存储顺序排列。</summary>
    public sealed record UploadRequest(Dictionary<int, List<RecordDto>> Pools);

    public static void MapUids(this IEndpointRouteBuilder api)
    {
        var uids = api.MapGroup("/uids").RequireAuthorization();

        uids.MapPost("/verify", async (LinkRequest body, HttpContext http, GachaImportService import, CancellationToken token) =>
        {
            Guid userId = http.User.UserId()!.Value;
            try
            {
                return Results.Ok(await import.VerifyAndImportAsync(userId, body.Url, null, token));
            }
            catch (UserFacingException ex)
            {
                return AuthEndpoints.Problem(ex.Message);
            }
        }).RequireRateLimiting(RateLimits.Gacha);

        // 用游戏账号绑定：浏览器登录游戏后上报账号资料，按其中的 UID 绑定（必要时从原账号转移）并保存资料。
        // 游戏登录只支持国际服，绑定的区服记为 global。
        uids.MapPost("/claim", async (ProfileUpload body, HttpContext http, UidBindingService bindings, ProfileService profiles, CancellationToken token) =>
        {
            try
            {
                ProfileService.Parsed parsed = ProfileService.Parse(body, null);
                bool transferred = await bindings.BindUidAsync(http.User.UserId()!.Value, parsed.Uid, "global", "game", token);
                await profiles.SaveAsync(parsed, token);
                return Results.Ok(new { uid = parsed.Uid, transferred });
            }
            catch (UserFacingException ex)
            {
                return AuthEndpoints.Problem(ex.Message);
            }
        }).RequireRateLimiting(RateLimits.Profile);

        var one = uids.MapGroup("/{uid}").AddEndpointFilter(RequireOwnedUid);
        one.MapUidRoles();

        one.MapPost("/gacha/refresh", async (string uid, LinkRequest body, HttpContext http, GachaImportService import, CancellationToken token) =>
        {
            try
            {
                return Results.Ok(await import.VerifyAndImportAsync(http.User.UserId()!.Value, body.Url, uid, token));
            }
            catch (UserFacingException ex)
            {
                return AuthEndpoints.Problem(ex.Message);
            }
        }).RequireRateLimiting(RateLimits.Gacha);

        // 上传浏览器本地（未登录时导入）的记录，合并进这个 UID
        one.MapPost("/gacha/upload", async (string uid, UploadRequest body, GachaImportService import, CancellationToken token) =>
        {
            try
            {
                return Results.Ok(new { added = await import.UploadAsync(uid, GuestEndpoints.ToPools(uid, body.Pools), token) });
            }
            catch (UserFacingException ex)
            {
                return AuthEndpoints.Problem(ex.Message);
            }
        }).RequireRateLimiting(RateLimits.Gacha).WithMetadata(new RequestSizeLimitAttribute(Limits.MaxRecordsBody));

        one.MapGet("/gacha", async (string uid, string? lang, GachaQueries queries, CancellationToken token) =>
            Results.Ok(GachaMapper.Overview(await queries.LoadAsync(uid, lang, token))));

        one.MapGet("/gacha/pools/{pool:int}/records", async (string uid, int pool, IGachaRepository gacha, CancellationToken token) =>
        {
            if (!Enum.IsDefined(typeof(CardPoolType), pool)) return Results.NotFound();
            IReadOnlyList<GachaPull> records = await gacha.GetPoolRecordsByUidAsync(uid, pool, token);
            return Results.Ok(records.Select(GachaMapper.Record));
        });

        one.MapGet("/gacha/charts", async (string uid, string? pools, string? since, string? lang, GachaQueries queries, CancellationToken token) =>
        {
            PullsSnapshot snapshot = await queries.LoadAsync(uid, lang, token);
            HashSet<CardPoolType>? poolSet = string.IsNullOrWhiteSpace(pools)
                ? null
                : pools.Split(',').Select(p => int.TryParse(p, out int v) ? v : -1).Where(v => Enum.IsDefined(typeof(CardPoolType), v)).Select(v => (CardPoolType)v).ToHashSet();
            DateTime? sinceDate = DateTime.TryParse(since, out DateTime d) ? d : null;
            return Results.Ok(GachaMapper.Chart(PullsChartBuilder.Build(snapshot, new PullsChartFilter(poolSet, sinceDate))));
        });

        one.MapDelete("/gacha", async (string uid, IGachaRepository gacha, CancellationToken token) =>
            Results.Ok(new { removed = await gacha.DeleteByUidAsync(uid, token) }));

        // 账号资料：浏览器转交（不可信，只给本人看）
        one.MapPost("/profile", async (string uid, ProfileUpload body, ProfileService profiles, CancellationToken token) =>
        {
            try
            {
                await profiles.SaveAsync(ProfileService.Parse(body, uid), token);
                return Results.Ok();
            }
            catch (UserFacingException ex)
            {
                return AuthEndpoints.Problem(ex.Message);
            }
        }).RequireRateLimiting(RateLimits.Profile);

        one.MapGet("/profile", async (string uid, IPlayerInfoRepository players, IUserRepository users, IDbContextFactory<WebDbContext> webDb, CancellationToken token) =>
        {
            AccountSummary? account = await users.GetUserAccountAsync(uid, token);
            PlayerSnapshot? snapshot = await players.LoadPlayerRoleDataAsync(uid, token);
            await using WebDbContext db = await webDb.CreateDbContextAsync(token);
            UidDataSource? source = await db.DataSources.FindAsync([uid, "profile"], token);
            if (snapshot is null || source is null) return Results.Ok(new { uid, hasProfile = false });
            return Results.Ok(new
            {
                uid,
                hasProfile = true,
                updatedAt = source.UpdatedAt,
                account = account is null ? null : new { account.Name, account.Level, account.Region, account.HeadPhoto },
                snapshot = ProfileMapper.Map(snapshot),
            });
        });

        one.MapDelete("/", async (string uid, UidDataService data, CancellationToken token) =>
        {
            await data.DeleteUidAsync(uid, token);
            return Results.Ok();
        });
    }

    private static async ValueTask<object?> RequireOwnedUid(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        HttpContext http = context.HttpContext;
        if (http.User.UserId() is not { } userId) return Results.Unauthorized();
        string? uid = http.GetRouteValue("uid") as string;
        if (uid is null) return Results.NotFound();
        WebDbContext db = http.RequestServices.GetRequiredService<WebDbContext>();
        bool owned = await db.UidBindings.AnyAsync(b => b.Uid == uid && b.UserId == userId, http.RequestAborted);
        return owned ? await next(context) : Results.NotFound();
    }
}

/// <summary>读取一个 UID 的全部卡池并按桌面端同一计算链统计。</summary>
public sealed class GachaQueries(IGachaRepository gacha, IGachaStatisticsService statistics)
{
    public async Task<PullsSnapshot> LoadAsync(string uid, string? lang, CancellationToken token)
    {
        var pools = new Dictionary<CardPoolType, IReadOnlyList<GachaPull>>();
        foreach (CardPoolType type in Enum.GetValues<CardPoolType>())
            pools[type] = await gacha.GetPoolRecordsByUidAsync(uid, (int)type, token);
        string language = lang is "en" or "ja" ? lang : "zh-Hans";
        return PullsService.Calculate(uid, pools, statistics, language);
    }
}

public static class ProfileMapper
{
    public static object Map(PlayerSnapshot s) => new
    {
    @base = s.Base is { } b ? new
        {
            b.Name, b.Id, b.CreatTime, b.ActiveDays, b.Level, b.WorldLevel, b.RoleNum, b.SoundBox,
            b.Energy, b.MaxEnergy, b.StoreEnergy, b.StoreEnergyRecoverTime, b.MaxStoreEnergy, b.EnergyRecoverTime,
            b.Liveness, b.LivenessMaxCount, b.LivenessUnlock, b.ChapterId, b.WeeklyInstCount,
            b.Boxes, b.BasicBoxes, b.PhantomBoxes, b.BirthMon, b.BirthDay,
        } : null,
        motor = s.MotorData is { } m ? new
        {
            m.Level, m.Exp, m.NextExp,
            skins = m.Skins?.Select(x => new { id = x.SkinId, x.Quality }),
            stickers = m.Stickers?.Select(x => new { x.Id, x.Quality, x.PartId }),
            decorations = m.Decorations?.Select(x => new { x.Id, x.Quality, x.PartId }),
            frames = m.Frames?.Select(x => new { x.Id, x.Quality }),
            equipSkin = m.EquipSkin is { } e ? new { id = e.SkinId, e.Quality } : null,
        } : null,
        music = s.MusicData?.Select(x => new { x.Id, x.Count, x.TotalCount }),
        battlePass = s.BattlePass is { } p ? new { p.Level, p.WeekExp, p.WeekMaxExp, p.IsUnlock, p.IsOpen, p.Exp, p.ExpLimit } : null,
    };
}
