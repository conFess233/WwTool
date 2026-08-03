using WwTool.Common.Models.ApiResponse;

namespace WwTool.Services.Repositories;

public interface IPlayerInfoRepository
{
    Task SavePlayerRegionInfoAsync(PlayerRegionInfo playerRegionInfo, string region, string oauthCode, CancellationToken cancellationToken = default);
    Task SavePlayerRoleDataAsync(string uid, RoleDetailInfo roleDetail, string playerRegion, PlayerRegionInfo playerRegionInfo, CancellationToken cancellationToken = default);
    Task<RoleDetailInfo?> LoadPlayerRoleDataAsync(string uid, CancellationToken cancellationToken = default);
}
