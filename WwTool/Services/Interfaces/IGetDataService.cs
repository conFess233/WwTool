using System;
using System.Collections.Generic;
using System.Text;
using WwTool.Common.Enums;
using WwTool.Common.Models;
using WwTool.Common.Models.Entities;
using WwTool.Common.Models.ApiRequest;
using WwTool.Common.Models.ApiResponse;

namespace WwTool.Services.Interfaces
{
    public interface IGetDataService
    {
        Task<IEnumerable<GachaData>> GetGachaLogAsync(GachaRequest param, GachaServerRegion serverRegion, CancellationToken cancellationToken = default);
        Task<GetUserInfoResponse?> GetUserInfoAsync(GetUserInfoRequest request, CancellationToken cancellationToken = default);
        Task<QueryPlayerInfoResponse?> QueryPlayerInfoAsync(QueryPlayerInfoRequest request, CancellationToken cancellationToken = default);
        Task<QueryRoleResponse?> QueryRoleAsync(QueryRoleRequest request, CancellationToken cancellationToken = default);

        Task<RoleDetailInfo?> GetRoleDetailAsync(string uid, bool forceRefresh = false, CancellationToken cancellationToken = default);

        Task<PlayerRegionInfo?> FetchAndSavePlayerRegionInfoAsync(string? uid = null, string? oauthCode = null, CancellationToken cancellationToken = default);
        Task<RoleDetailInfo?> FetchAndSaveRoleDetailAsync(string uid, string region, string? oauthCode = null, CancellationToken cancellationToken = default);
        Task SyncAllUserDataAsync(string? uid = null, string? oauthCode = null, CancellationToken cancellationToken = default);
    }
}
