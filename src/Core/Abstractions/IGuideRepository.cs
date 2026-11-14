using WwTool.Core.Models.Api.Responses;
using WwTool.Core.Models.Domain;

namespace WwTool.Core.Abstractions;

public sealed record GuideCredential(string CUid, string Token, string ServerId);

public sealed record GuideSnapshot(
    DateTimeOffset? LastSyncedAtUtc,
    IReadOnlyList<GuideRoleData> Roles,
    IReadOnlyList<GuideWeaponData> Weapons);

public interface IGuideRepository
{
    Task SaveCredentialAndPlayersAsync(string cUid, string token, IReadOnlyList<GuidePlayerIdentity> players, CancellationToken cancellationToken = default);
    /// <summary>只保存 UID 与攻略站玩家的对应关系，不保存令牌（Web 端：令牌只留在浏览器）。</summary>
    Task SavePlayersAsync(string cUid, IReadOnlyList<GuidePlayerIdentity> players, CancellationToken cancellationToken = default);
    Task<GuideCredential?> GetCredentialAsync(string uid, CancellationToken cancellationToken = default);
    Task DeleteCredentialAsync(string cUid, CancellationToken cancellationToken = default);
    Task ReplaceSnapshotAsync(string uid, IReadOnlyList<GuideRoleData> roles, IReadOnlyList<GuideWeaponData> weapons, DateTimeOffset syncedAtUtc, CancellationToken cancellationToken = default);
    Task<GuideSnapshot> LoadSnapshotAsync(string uid, CancellationToken cancellationToken = default);
}
