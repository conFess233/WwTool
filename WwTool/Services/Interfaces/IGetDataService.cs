using WwTool.Core.Models.Domain;
using System;
using System.Collections.Generic;
using System.Text;
using WwTool.Core.Enums;
using WwTool.Common.Models;
using WwTool.Core.Data.Entities;
using WwTool.Core.Models.Api.Requests;
using WwTool.Core.Models.Api.Responses;

namespace WwTool.Services.Interfaces
{
    public interface IGetDataService
    {
        Task<IEnumerable<GachaPull>> GetGachaLogAsync(GachaRequest param, GachaServerRegion serverRegion, CancellationToken cancellationToken = default);
        Task<GetUserInfoResponse?> GetUserInfoAsync(GetUserInfoRequest request, CancellationToken cancellationToken = default);
        Task<QueryPlayerInfoResponse?> QueryPlayerInfoAsync(QueryPlayerInfoRequest request, CancellationToken cancellationToken = default);
        Task<QueryRoleResponse?> QueryRoleAsync(QueryRoleRequest request, CancellationToken cancellationToken = default);

        Task<PlayerSnapshot?> GetRoleDetailAsync(string uid, bool forceRefresh = false, CancellationToken cancellationToken = default);

        Task<PlayerRegionSummary?> FetchAndSavePlayerRegionInfoAsync(string? uid = null, string? oauthCode = null, CancellationToken cancellationToken = default);
        Task<PlayerSnapshot?> FetchAndSaveRoleDetailAsync(string uid, string region, string? oauthCode = null, CancellationToken cancellationToken = default);
        Task SyncAllUserDataAsync(string? uid = null, string? oauthCode = null, CancellationToken cancellationToken = default);
    }
}
