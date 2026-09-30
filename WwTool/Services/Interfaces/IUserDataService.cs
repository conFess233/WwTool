using WwTool.Common.Models.Domain;
using WwTool.Common.Models.ApiResponse;

namespace WwTool.Services.Interfaces;

public interface IUserDataService
{
    event EventHandler<AccountDeletedEventArgs>? AccountDeleted;
    Task<IReadOnlyList<AccountSummary>> ListAccountsAsync(CancellationToken cancellationToken = default);
    Task<AcquisitionTimes> ReadAcquisitionTimesAsync(string uid, int[] pools, CancellationToken cancellationToken = default);
    Task<string?> GetCredentialAsync(string uid, CancellationToken cancellationToken = default);
    Task DeleteAccountAsync(string uid, CancellationToken cancellationToken = default);
    Task<PlayerSnapshot?> LoadRoleSnapshotAsync(string uid, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<GachaPull>> ReadGachaInSourceOrderAsync(string uid, int poolType, CancellationToken cancellationToken = default);
    Task<int> ImportGachaAsync(string uid, int poolType, IEnumerable<GachaPull> records, string source, CancellationToken cancellationToken = default);
}
