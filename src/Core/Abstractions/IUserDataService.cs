using WwTool.Core.Models.Domain;
using WwTool.Core.Models.Api.Responses;

namespace WwTool.Core.Abstractions;

public interface IUserDataService
{
    event EventHandler<AccountDeletedEventArgs>? AccountDeleted;
    Task<IReadOnlyList<AccountSummary>> ListAccountsAsync(CancellationToken cancellationToken = default);
    Task<AcquisitionTimes> ReadAcquisitionTimesAsync(string uid, int[] pools, CancellationToken cancellationToken = default);
    Task<string?> GetCredentialAsync(string uid, CancellationToken cancellationToken = default);
    Task DeleteAccountAsync(string uid, CancellationToken cancellationToken = default);
    Task<PlayerSnapshot?> LoadRoleSnapshotAsync(string uid, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<GachaPull>> ReadGachaInSourceOrderAsync(string uid, int poolType, CancellationToken cancellationToken = default);
    /// <summary>清空该账号的本地抽卡记录，返回删除条数。</summary>
    Task<int> ClearGachaAsync(string uid, CancellationToken cancellationToken = default);
    Task<int> ImportGachaAsync(string uid, int poolType, IEnumerable<GachaPull> records, string source, CancellationToken cancellationToken = default);
}
