using System;
using System.Collections.Generic;
using System.Text;
using WwTool.Common.Models;
using WwTool.Common.Models.ApiRequest;
using WwTool.Common.Models.ApiResponse;

namespace WwTool.Services.Interfaces
{
    public interface IGetDataService
    {
        Task<IEnumerable<GachaData>> GetGachaLogAsync(GachaRequest param);
        Task<GetUserInfoResponse?> GetUserInfoAsync(GetUserInfoRequest request);
        Task<QueryPlayerInfoResponse?> QueryPlayerInfoAsync(QueryPlayerInfoRequest request);
        Task<QueryRoleResponse?> QueryRoleAsync(QueryRoleRequest request);

        Task<RoleDetailInfo?> GetRoleDetailAsync(string uid, bool forceRefresh = false);

        Task<PlayerRegionInfo?> FetchAndSavePlayerRegionInfoAsync(string? uid = null, string? oauthCode = null);
        Task<RoleDetailInfo?> FetchAndSaveRoleDetailAsync(string uid, string region, string? oauthCode = null);
        Task SyncAllUserDataAsync(string? uid = null, string? oauthCode = null);
    }
}
