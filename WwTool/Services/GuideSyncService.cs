using System.Collections.Concurrent;
using System.Text.Json;
using WwTool.Common.Exceptions;
using WwTool.Common.Models.ApiRequest;
using WwTool.Common.Models.ApiResponse;
using WwTool.Common.Models.Entities;
using WwTool.Services.Interfaces;

namespace WwTool.Services;

public sealed class GuideSyncService(
    IGuideApiClient apiClient,
    IGuideRepository repository) : IGuideSyncService
{
    private static readonly JsonSerializerOptions DetailJsonOptions = new(JsonSerializerDefaults.Web);

    public async Task CaptureSessionAsync(string cUid, string cName, string accessToken, string language, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(cUid) || string.IsNullOrWhiteSpace(accessToken))
            throw new GuideAuthenticationRequiredException("当前登录上下文不完整，请重新登录。");
        string token = await apiClient.LoginAsync(new GuideLoginRequest
        {
            CUid = cUid,
            CName = cName,
            AccessToken = accessToken
        }, language, cancellationToken);
        IReadOnlyList<GuidePlayer> players = await apiClient.GetPlayersAsync(token, language, cancellationToken);
        await repository.SaveCredentialAndPlayersAsync(cUid, token, players, cancellationToken);
    }

    public async Task SyncAsync(string uid, string language, CancellationToken cancellationToken = default)
    {
        GuideCredential credential = await repository.GetCredentialAsync(uid, cancellationToken)
            ?? throw new GuideAuthenticationRequiredException("尚未保存该账号的 Guide 登录状态，请重新登录。");
        try
        {
            IReadOnlyList<GuidePlayer> players = await apiClient.GetPlayersAsync(credential.Token, language, cancellationToken);
            GuidePlayer player = players.FirstOrDefault(x => x.PlayerId?.ToString(System.Globalization.CultureInfo.InvariantCulture) == uid)
                ?? throw new GuideApiException("Guide 账号中未找到当前 UID。");
            if (string.IsNullOrWhiteSpace(player.ServerId))
                throw new GuideApiException("Guide 玩家记录缺少服务器标识。");
            await repository.SaveCredentialAndPlayersAsync(credential.CUid, credential.Token, players, cancellationToken);
            await apiClient.ChoosePlayerAsync(credential.Token, language, new GuideChoosePlayerRequest
            {
                PlayerId = player.PlayerId!.Value,
                ServerId = player.ServerId
            }, cancellationToken);

            IReadOnlyList<GuideAvatar> avatars = await apiClient.GetAvatarsAsync(credential.Token, language, cancellationToken);
            var owned = avatars.Select((avatar, index) => (avatar, index)).Where(x => x.avatar.IsAcquired).ToList();
            var results = new ConcurrentDictionary<int, (GuideRoleSnapshot Role, GuideEquippedWeaponSnapshot? Weapon)>();
            using var gate = new SemaphoreSlim(2, 2);
            Task[] tasks = owned.Select(async entry =>
            {
                await gate.WaitAsync(cancellationToken);
                try
                {
                    results[entry.index] = await LoadRoleAsync(uid, credential.Token, language, entry.avatar, entry.index, cancellationToken);
                }
                finally
                {
                    gate.Release();
                }
            }).ToArray();
            await Task.WhenAll(tasks);

            List<GuideRoleSnapshot> roles = avatars.Select((avatar, index) =>
                results.TryGetValue(index, out var loaded)
                    ? loaded.Role
                    : CreateRoleSnapshot(uid, avatar, index)).ToList();
            List<GuideEquippedWeaponSnapshot> weapons = results.OrderBy(x => x.Key).Where(x => x.Value.Weapon is not null).Select(x => x.Value.Weapon!).ToList();
            await repository.ReplaceSnapshotAsync(uid, roles, weapons, DateTimeOffset.UtcNow, cancellationToken);
        }
        catch (GuideAuthenticationRequiredException)
        {
            await repository.DeleteCredentialAsync(credential.CUid, CancellationToken.None);
            throw;
        }
    }

    private async Task<(GuideRoleSnapshot Role, GuideEquippedWeaponSnapshot? Weapon)> LoadRoleAsync(
        string uid, string token, string language, GuideAvatar avatar, int sourceOrder, CancellationToken cancellationToken)
    {
        IReadOnlyList<GuideIntroductionSummary> introductions = await apiClient.GetIntroductionsAsync(token, language, avatar.RoleGbId, cancellationToken);
        IEnumerable<GuideIntroductionSummary> candidates = introductions
            .Where(x => x.Texts.Any(t => string.Equals(t.Language, language, StringComparison.OrdinalIgnoreCase)))
            .Concat(introductions)
            .DistinctBy(x => x.Id);

        GuideIntroductionDetail? detail = null;
        GuideIntroductionSummary? selected = null;
        foreach (GuideIntroductionSummary candidate in candidates)
        {
            detail = await apiClient.GetIntroductionAsync(token, language, avatar.RoleGbId, candidate.Id, cancellationToken);
            if (detail is not null)
            {
                selected = candidate;
                break;
            }
        }
        if (detail is null || selected is null)
            throw new GuideApiException($"角色 {avatar.RoleGbId} 的所有攻略方案均未返回详情。");

        GuideRoleSnapshot role = CreateRoleSnapshot(uid, avatar, sourceOrder);
        role.Sequence = detail.RoleResonance?.Items.Count(x => x.IsAcquired) ?? 0;
        role.StrategyId = selected.Id;
        role.StrategyModifiedAt = selected.ModifiedAt;
        role.DetailJson = JsonSerializer.Serialize(detail, DetailJsonOptions);
        GuideWeapon? current = detail.Weapon?.Current;
        GuideEquippedWeaponSnapshot? weapon = current is null || string.IsNullOrWhiteSpace(current.GbId) ? null : new GuideEquippedWeaponSnapshot
        {
            Uid = uid,
            OwnerRoleGbId = avatar.RoleGbId,
            WeaponGbId = current.GbId,
            PictureUrl = current.PictureUrl,
            Star = current.Star,
            SourceOrder = sourceOrder
        };
        return (role, weapon);
    }

    private static GuideRoleSnapshot CreateRoleSnapshot(string uid, GuideAvatar avatar, int sourceOrder) => new()
    {
        Uid = uid,
        RoleGbId = avatar.RoleGbId,
        SourceOrder = sourceOrder,
        CardPictureUrl = avatar.CardPictureUrl,
        IllustrationPictureUrl = avatar.IllustrationPictureUrl,
        Star = avatar.Star,
        RoleStatus = avatar.RoleStatus,
        Sequence = 0,
        IsAcquired = avatar.IsAcquired,
        MayRoleGbId = avatar.MayRoleGbId
    };
}
