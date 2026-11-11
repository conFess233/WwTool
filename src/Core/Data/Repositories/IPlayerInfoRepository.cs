using WwTool.Core.Models.Domain;
using WwTool.Core.Models.Api.Responses;

namespace WwTool.Core.Data.Repositories;

public interface IPlayerInfoRepository
{
    Task SavePlayerRegionInfoAsync(PlayerRegionSummary playerRegionInfo, string region, string oauthCode, CancellationToken cancellationToken = default);
    Task SavePlayerRoleDataAsync(string uid, PlayerSnapshot roleDetail, string playerRegion, PlayerRegionSummary playerRegionInfo, CancellationToken cancellationToken = default);
    Task<PlayerSnapshot?> LoadPlayerRoleDataAsync(string uid, CancellationToken cancellationToken = default);
}
