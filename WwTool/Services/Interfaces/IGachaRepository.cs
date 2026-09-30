using WwTool.Common.Models.Domain;
using WwTool.Common.Models.ApiResponse;

namespace WwTool.Services.Repositories;

public interface IGachaRepository
{
    Task<WwTool.Common.Models.Domain.AcquisitionTimes> ReadAcquisitionTimesAsync(string uid, int[] pools, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<GachaPull>> GetAllRecordsByUidAsync(string uid, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<GachaPull>> GetPoolRecordsByUidAsync(string uid, int poolType, CancellationToken cancellationToken = default);
    Task<int> SyncGachaDataAsync(
        string uid,
        int poolType,
        IEnumerable<GachaPull> records,
        string source = "remote",
        CancellationToken cancellationToken = default);
}
