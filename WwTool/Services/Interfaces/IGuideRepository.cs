using WwTool.Common.Models.ApiResponse;
using WwTool.Common.Models.Entities;

namespace WwTool.Services.Interfaces;

public sealed record GuideCredential(string CUid, string Token, string ServerId);

public sealed record GuideSnapshot(
    DateTimeOffset? LastSyncedAtUtc,
    IReadOnlyList<GuideRoleSnapshot> Roles,
    IReadOnlyList<GuideEquippedWeaponSnapshot> Weapons);

public interface IGuideRepository
{
    Task SaveCredentialAndPlayersAsync(string cUid, string token, IReadOnlyList<GuidePlayer> players, CancellationToken cancellationToken = default);
    Task<GuideCredential?> GetCredentialAsync(string uid, CancellationToken cancellationToken = default);
    Task DeleteCredentialAsync(string cUid, CancellationToken cancellationToken = default);
    Task ReplaceSnapshotAsync(string uid, IReadOnlyList<GuideRoleSnapshot> roles, IReadOnlyList<GuideEquippedWeaponSnapshot> weapons, DateTimeOffset syncedAtUtc, CancellationToken cancellationToken = default);
    Task<GuideSnapshot> LoadSnapshotAsync(string uid, CancellationToken cancellationToken = default);
}
