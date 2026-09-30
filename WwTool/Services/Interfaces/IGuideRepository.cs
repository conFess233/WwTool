using WwTool.Common.Models.ApiResponse;
using WwTool.Common.Models.Domain;

namespace WwTool.Services.Interfaces;

public sealed record GuideCredential(string CUid, string Token, string ServerId);

public sealed record GuideSnapshot(
    DateTimeOffset? LastSyncedAtUtc,
    IReadOnlyList<GuideRoleData> Roles,
    IReadOnlyList<GuideWeaponData> Weapons);

public interface IGuideRepository
{
    Task SaveCredentialAndPlayersAsync(string cUid, string token, IReadOnlyList<GuidePlayerIdentity> players, CancellationToken cancellationToken = default);
    Task<GuideCredential?> GetCredentialAsync(string uid, CancellationToken cancellationToken = default);
    Task DeleteCredentialAsync(string cUid, CancellationToken cancellationToken = default);
    Task ReplaceSnapshotAsync(string uid, IReadOnlyList<GuideRoleData> roles, IReadOnlyList<GuideWeaponData> weapons, DateTimeOffset syncedAtUtc, CancellationToken cancellationToken = default);
    Task<GuideSnapshot> LoadSnapshotAsync(string uid, CancellationToken cancellationToken = default);
}
