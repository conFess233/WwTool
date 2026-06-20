using System.Threading.Tasks;
using WwTool.Common.Models.ApiResponse;

namespace WwTool.Services.Repositories
{
    public interface IPlayerInfoRepository
    {
        Task SavePlayerRegionInfoAsync(PlayerRegionInfo playerRegionInfo, string region, string oauthCode);
        Task SavePlayerRoleDataAsync(string uid, RoleDetailInfo roleDetail, string playerRegion, PlayerRegionInfo playerRegionInfo);
        Task<RoleDetailInfo?> LoadPlayerRoleDataAsync(string uid);
    }
}
