using WwTool.Common.Models.Domain;
using WwTool.Common.Models.ApiResponse;

namespace WwTool.Services.Interfaces;

public interface IUserDataService
{
    Task<IReadOnlyList<AccountSummary>> ListAccountsAsync(CancellationToken cancellationToken = default);
    Task<string?> GetCredentialAsync(string uid, CancellationToken cancellationToken = default);
    Task DeleteAccountAsync(string uid, CancellationToken cancellationToken = default);
    Task<RoleDetailInfo?> LoadRoleSnapshotAsync(string uid, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<GachaData>> ReadGachaInSourceOrderAsync(string uid, int poolType, CancellationToken cancellationToken = default);
    Task<int> ImportGachaAsync(string uid, int poolType, IEnumerable<GachaData> records, string source, CancellationToken cancellationToken = default);
}
