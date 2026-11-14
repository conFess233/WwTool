using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json;
using WwTool.Core.Abstractions;
using WwTool.Core.Abstractions.Platform;
using WwTool.Core.Data.Repositories;
using WwTool.Core.Exceptions;
using WwTool.Core.Models.Domain;
using WwTool.Core.Services;
using WwTool.Core.Services.Guide;
using WwTool.Web.Auth;
using WwTool.Web.Uids;

namespace WwTool.Web.Roles;

public sealed record RoleDto(
    string Id, string Name, int Star, bool Limited, int Sequence, string? CardPictureUrl, string? IllustrationPictureUrl,
    DateTime? AcquiredAt, IReadOnlyList<GuideHoverAttribute> Attributes, WeaponDto? Weapon, GuideRoleBuild Build);

public sealed record WeaponDto(string Id, string Name, int Star, IReadOnlyList<GuideHoverAttribute> Fields);

public sealed record EquippedWeaponDto(string Id, string Name, int Star, bool Limited, string OwnerId, string OwnerName, DateTime? AcquiredAt);

public sealed record RolesDto(DateTimeOffset? SyncedAt, IReadOnlyList<RoleDto> Roles, IReadOnlyList<EquippedWeaponDto> Weapons, bool HasGachaRecords);

public static class RoleEndpoints
{
    public sealed record TokenRequest(string? CUid, string? CName, string? AccessToken, string? Lang);
    public sealed record SyncRequest(string? Token, string? CUid, string? Lang);

    public static void MapRoles(this IEndpointRouteBuilder api)
    {
        // 用游戏登录得到的 accessToken 换 Guide Token：后端不保存、不记录，换得后直接返回给浏览器
        api.MapPost("/guide/token", async (TokenRequest body, RoleService roles, CancellationToken token) =>
        {
            try
            {
                string guideToken = await roles.LoginAsync(body.CUid ?? string.Empty, body.CName ?? string.Empty, body.AccessToken ?? string.Empty, Language(body.Lang), token);
                return Results.Ok(new { token = guideToken });
            }
            catch (UserFacingException ex)
            {
                return AuthEndpoints.Problem(ex.Message);
            }
        }).RequireAuthorization().RequireRateLimiting(RateLimits.Profile);
    }

    /// <summary>挂在 <c>/api/uids/{uid}</c> 组下（已校验 UID 属于当前站点账号）。</summary>
    public static void MapUidRoles(this RouteGroupBuilder one)
    {
        one.MapPost("/roles/sync", async (string uid, SyncRequest body, RoleService roles, CancellationToken token) =>
        {
            try
            {
                int count = await roles.SyncAsync(uid, body.Token ?? string.Empty, body.CUid ?? string.Empty, Language(body.Lang), token);
                return Results.Ok(new { roles = count });
            }
            catch (GuideAuthenticationRequiredException)
            {
                // 浏览器据此删除本地保存的 Guide Token 并提示重新登录游戏账号
                return Results.Problem(detail: "攻略站授权已失效，请重新登录游戏账号。", statusCode: StatusCodes.Status401Unauthorized, title: "guide-auth");
            }
            catch (UserFacingException ex)
            {
                return AuthEndpoints.Problem(ex.Message);
            }
        }).RequireRateLimiting(RateLimits.Profile);

        one.MapGet("/roles", async (string uid, string? lang, IGuideRepository repository, IGachaRepository gacha, GameDataService catalog, IAppPaths paths, ILoggerService logger, CancellationToken token) =>
        {
            string language = Language(lang);
            GuideSnapshot snapshot = await repository.LoadSnapshotAsync(uid, token);
            AcquisitionTimes roleTimes = await gacha.ReadAcquisitionTimesAsync(uid, GuideCardPools.Roles.Select(x => (int)x).ToArray(), token);
            AcquisitionTimes weaponTimes = await gacha.ReadAcquisitionTimesAsync(uid, GuideCardPools.Weapons.Select(x => (int)x).ToArray(), token);
            Func<string, string> text = LanguageText.For(paths, language);
            string Name(string id) => int.TryParse(id, out int n) ? catalog.GetItemById(n)?.GetName(language) ?? id : id;
            // 与桌面端相同：只有限定五星有可靠的首次获取时间
            bool Limited(string id, int star, bool character) => star == 5 && int.TryParse(id, out int n) && catalog.GetLimitedStatus(n, star, character) == true;
            DateTime? Time(AcquisitionTimes times, string id, bool limited) =>
                limited && int.TryParse(id, out int n) && times.Times.TryGetValue(n, out DateTime at) ? at : null;
            void Warn(JsonException ex) => logger.Warn("角色养成快照无法解析。", ex);

            var roles = snapshot.Roles.Select(role =>
            {
                GuideWeaponData? equipped = snapshot.Weapons.FirstOrDefault(w => w.OwnerRoleGbId == role.RoleGbId);
                GuideRoleHoverDetails hover = GuideRoleHoverMapper.Map(role.DetailJson, equipped, catalog, language, text, Warn);
                GuideRoleBuild build = GuideRoleHoverMapper.MapBuild(role.DetailJson, language, Warn);
                int sequence = GuideRoleHoverMapper.ResolveSequence(role, (message, ex) => logger.Warn(message, ex));
                WeaponDto? weapon = hover.Weapon is { } w && equipped is not null
                    ? new WeaponDto(equipped.WeaponGbId, w.Name, w.Star, w.Fields)
                    : null;
                bool limited = Limited(role.RoleGbId, role.Star, true);
                return new RoleDto(role.RoleGbId, Name(role.RoleGbId), role.Star, limited, sequence, role.CardPictureUrl, role.IllustrationPictureUrl,
                    Time(roleTimes, role.RoleGbId, limited), hover.Attributes, weapon, build);
            }).ToList();
            var weapons = snapshot.Weapons.Select(w =>
            {
                bool limited = Limited(w.WeaponGbId, w.Star, false);
                return new EquippedWeaponDto(w.WeaponGbId, Name(w.WeaponGbId), w.Star, limited, w.OwnerRoleGbId, Name(w.OwnerRoleGbId), Time(weaponTimes, w.WeaponGbId, limited));
            }).ToList();
            return Results.Ok(new RolesDto(snapshot.LastSyncedAtUtc, roles, weapons, roleTimes.HasRecords || weaponTimes.HasRecords));
        });
    }

    private static string Language(string? lang) => lang is "en" or "ja" ? lang : "zh-Hans";
}

/// <summary>按语言读取 Core 的界面文案（<c>Language/{lang}.json</c>），找不到时回退到中文、再回退到键名。</summary>
public static class LanguageText
{
    private static readonly ConcurrentDictionary<string, Dictionary<string, string>> Tables = new();

    public static Func<string, string> For(IAppPaths paths, string language)
    {
        Dictionary<string, string> table = Load(paths, language);
        Dictionary<string, string> fallback = Load(paths, "zh-Hans");
        return key => table.TryGetValue(key, out string? v) ? v : fallback.TryGetValue(key, out v) ? v : key;
    }

    private static Dictionary<string, string> Load(IAppPaths paths, string language) => Tables.GetOrAdd(language, lang =>
    {
        string file = Path.Combine(paths.BundledResourceDirectory, "Language", lang + ".json");
        return File.Exists(file) ? JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(file)) ?? [] : [];
    });
}
