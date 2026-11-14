using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json;
using WwTool.Core.Abstractions;
using WwTool.Core.Data.Repositories;
using WwTool.Core.Exceptions;
using WwTool.Core.Models.Api.Requests;
using WwTool.Core.Models.Api.Responses;
using WwTool.Core.Models.Domain;
using WwTool.Core.Services;
using WwTool.Core.Services.Guide;
using WwTool.Web.Uids;

namespace WwTool.Web.Roles;

/// <summary>
/// 角色数据（官方攻略站 guide-server）。该接口只允许攻略站自己的域名跨域访问，浏览器无法直连，由后端无状态转发：
/// <list type="bullet">
/// <item>游戏登录时浏览器把 accessToken 交给后端换一次 Guide Token，换得后立即返回，后端不保存、不写日志。</item>
/// <item>Guide Token 只加密保存在浏览器；每次同步由浏览器带上，后端用完即弃。</item>
/// <item>同步流程与桌面端 <see cref="GuideSyncService.SyncAsync"/> 相同，只把角色快照落库，不落令牌。</item>
/// </list>
/// </summary>
public sealed class RoleService(IGuideApiClient guide, IGuideRepository repository, ILoggerService logger)
{
    private static readonly JsonSerializerOptions DetailJsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> UidLocks = new();

    /// <summary>用游戏登录得到的 accessToken 换 Guide Token。</summary>
    public async Task<string> LoginAsync(string cUid, string cName, string accessToken, string language, CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(cUid) || string.IsNullOrWhiteSpace(accessToken) || cUid.Length > 64 || cName.Length > 128 || accessToken.Length > 512)
            throw new UserFacingException("游戏登录信息不完整，请重新登录游戏账号。");
        try
        {
            return await guide.LoginAsync(new GuideLoginRequest { CUid = cUid, CName = cName, AccessToken = accessToken }, language, token);
        }
        catch (Exception ex) when (ex is GuideApiException or GuideAuthenticationRequiredException or HttpRequestException or TaskCanceledException && !token.IsCancellationRequested)
        {
            logger.Warn($"攻略站登录失败：{ex.GetType().Name}");
            throw new UserFacingException("攻略站登录失败，请稍后重试。");
        }
    }

    /// <summary>同步一个 UID 的角色快照。令牌失效时抛 <see cref="GuideAuthenticationRequiredException"/>，由调用方提示重新登录。</summary>
    /// <param name="cUid">库洛账号 ID（不是凭据），用于记录 UID 属于哪个攻略站账号。</param>
    public async Task<int> SyncAsync(string uid, string guideToken, string cUid, string language, CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(guideToken) || guideToken.Length > 2048) throw new GuideAuthenticationRequiredException("缺少攻略站授权。");
        SemaphoreSlim gate = UidLocks.GetOrAdd(uid, _ => new SemaphoreSlim(1, 1));
        if (!await gate.WaitAsync(0, token)) throw new UserFacingException("这个 UID 正在同步，请稍候。");
        try
        {
            IReadOnlyList<GuidePlayer> players = await guide.GetPlayersAsync(guideToken, language, token);
            GuidePlayer player = players.FirstOrDefault(x => x.PlayerId?.ToString(CultureInfo.InvariantCulture) == uid)
                ?? throw new UserFacingException("登录的游戏账号中没有这个 UID。");
            if (string.IsNullOrWhiteSpace(player.ServerId)) throw new UserFacingException("攻略站返回的数据不完整，请稍后重试。");

            await repository.SavePlayersAsync(cUid.Length is > 0 and <= 64 ? cUid : "web", players.Select(x => new GuidePlayerIdentity(x.PlayerId, x.ServerId)).ToList(), token);
            await guide.ChoosePlayerAsync(guideToken, language, new GuideChoosePlayerRequest { PlayerId = player.PlayerId!.Value, ServerId = player.ServerId }, token);

            IReadOnlyList<GuideAvatar> avatars = await guide.GetAvatarsAsync(guideToken, language, token);
            var results = new ConcurrentDictionary<int, (GuideRoleData Role, GuideWeaponData? Weapon)>();
            using var limiter = new SemaphoreSlim(2, 2);
            await Task.WhenAll(avatars.Select((avatar, index) => (avatar, index)).Where(x => x.avatar.IsAcquired).Select(async entry =>
            {
                await limiter.WaitAsync(token);
                try
                {
                    results[entry.index] = await LoadRoleAsync(uid, guideToken, language, entry.avatar, entry.index, token);
                }
                finally
                {
                    limiter.Release();
                }
            }));

            List<GuideRoleData> roles = avatars.Select((avatar, index) =>
                results.TryGetValue(index, out var loaded) ? loaded.Role : Snapshot(uid, avatar, index)).ToList();
            List<GuideWeaponData> weapons = results.OrderBy(x => x.Key).Where(x => x.Value.Weapon is not null).Select(x => x.Value.Weapon!).ToList();
            await repository.ReplaceSnapshotAsync(uid, roles, weapons, DateTimeOffset.UtcNow, token);
            return results.Count;
        }
        catch (GuideApiException ex)
        {
            logger.Warn($"攻略站同步失败：UID {UidBindingService.Mask(uid)}，{ex.Message}");
            throw new UserFacingException("攻略站暂时无法访问，请稍后重试。");
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !token.IsCancellationRequested)
        {
            throw new UserFacingException("攻略站暂时无法访问，请稍后重试。");
        }
        finally
        {
            gate.Release();
        }
    }

    /// <summary>与 <see cref="GuideSyncService"/> 相同：依次尝试攻略方案，取第一个有详情的。</summary>
    private async Task<(GuideRoleData, GuideWeaponData?)> LoadRoleAsync(string uid, string guideToken, string language, GuideAvatar avatar, int order, CancellationToken token)
    {
        IReadOnlyList<GuideIntroductionSummary> introductions = await guide.GetIntroductionsAsync(guideToken, language, avatar.RoleGbId, token);
        IEnumerable<GuideIntroductionSummary> candidates = introductions
            .Where(x => x.Texts.Any(t => string.Equals(t.Language, language, StringComparison.OrdinalIgnoreCase)))
            .Concat(introductions)
            .DistinctBy(x => x.Id);
        GuideIntroductionDetail? detail = null;
        GuideIntroductionSummary? selected = null;
        foreach (GuideIntroductionSummary candidate in candidates)
        {
            detail = await guide.GetIntroductionAsync(guideToken, language, avatar.RoleGbId, candidate.Id, token);
            if (detail is not null)
            {
                selected = candidate;
                break;
            }
        }
        GuideRoleData role = Snapshot(uid, avatar, order);
        if (detail is null || selected is null) return (role, null);
        role.Sequence = detail.RoleResonance?.Items.Count(x => x.IsAcquired) ?? 0;
        role.StrategyId = selected.Id;
        role.StrategyModifiedAt = selected.ModifiedAt;
        role.DetailJson = JsonSerializer.Serialize(detail, DetailJsonOptions);
        GuideWeapon? current = detail.Weapon?.Current;
        GuideWeaponData? weapon = current is null || string.IsNullOrWhiteSpace(current.GbId) ? null : new GuideWeaponData
        {
            Uid = uid, OwnerRoleGbId = avatar.RoleGbId, WeaponGbId = current.GbId, PictureUrl = current.PictureUrl, Star = current.Star, SourceOrder = order,
        };
        return (role, weapon);
    }

    private static GuideRoleData Snapshot(string uid, GuideAvatar avatar, int order) => new()
    {
        Uid = uid, RoleGbId = avatar.RoleGbId, SourceOrder = order, CardPictureUrl = avatar.CardPictureUrl,
        IllustrationPictureUrl = avatar.IllustrationPictureUrl, Star = avatar.Star, RoleStatus = avatar.RoleStatus,
        Sequence = 0, IsAcquired = avatar.IsAcquired, MayRoleGbId = avatar.MayRoleGbId,
    };
}
