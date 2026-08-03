using WwTool.Common.Models.ApiResponse;

namespace WwTool.Services.Repositories;

public interface IGachaRepository
{
    Task<IReadOnlyList<GachaData>> GetAllRecordsByUidAsync(string uid, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<GachaData>> GetPoolRecordsByUidAsync(string uid, int poolType, CancellationToken cancellationToken = default);
    Task<int> SyncGachaDataAsync(
        string uid,
        int poolType,
        IEnumerable<GachaData> records,
        string source = "remote",
        CancellationToken cancellationToken = default);
}
