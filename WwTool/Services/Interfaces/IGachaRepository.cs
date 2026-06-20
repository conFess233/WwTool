using System.Collections.Generic;
using System.Threading.Tasks;
using WwTool.Common.Models;
using WwTool.Common.Models.ApiResponse;

namespace WwTool.Services.Repositories
{
    public interface IGachaRepository
    {
        (string? LatestTime, int Count) GetLatestRecordInfo(string uid, int poolType);
        void DeleteRecordsAtTime(string uid, int poolType, string time);
        void InsertRecords(IEnumerable<GachaData> records, string uid, int poolType);
        Task<List<GachaData>> GetAllRecordsByUid(string uid);
        Task<List<GachaData>> GetPoolRecordsByUid(string uid, int poolType);
        Task<int> SyncGachaDataAsync(string uid, int poolType, IEnumerable<GachaData>? data);
    }
}
