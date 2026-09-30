using WwTool.Common.Models.Domain;
using System.Security.Cryptography;
using WwTool.Common.Enums;
using WwTool.Common.Exceptions;
using WwTool.Common.Models;
using WwTool.Common.Models.Entities;
using WwTool.Common.Models.ApiRequest;
using WwTool.Common.Models.ApiResponse;
using WwTool.Services.Interfaces;
using WwTool.Services.Repositories;
using static WwTool.Services.LoginService;

namespace WwTool.Services
{
    /// <summary>
    /// 数据获取服务
    /// </summary>
    public class GetDataService : IGetDataService
    {
        private readonly IHttpService _apiService;
        private readonly IConfigService _configService;
        private readonly ILoginService _loginService;
        private readonly IUserRepository _userRepository;
        private readonly IPlayerInfoRepository _playerInfoRepository;
        private readonly ILoggerService _logger;

        public GetDataService(
            IHttpService apiService,
            IConfigService configService,
            ILoginService loginService,
            IUserRepository userRepository,
            IPlayerInfoRepository playerInfoRepository,
            ILoggerService logger)
        {
            _apiService = apiService;
            _configService = configService;
            _loginService = loginService;
            _userRepository = userRepository;
            _playerInfoRepository = playerInfoRepository;
            _logger = logger;
        }

        /// <summary>
        /// 获取指定账号的抽卡记录
        /// </summary>
        /// <param name="req">请求体</param>
        /// <returns>抽卡数据列表</returns>
        /// <exception cref="WwToolApiException"></exception>
        public async Task<IEnumerable<GachaPull>> GetGachaLogAsync(GachaRequest req, GachaServerRegion serverRegion, CancellationToken cancellationToken = default)
        {
            _logger.Info($"开始获取抽卡记录 (卡池类型: {req.CardPoolType})");
            string endpoint = serverRegion == GachaServerRegion.International
                ? _configService.Api.Urls.GachaUrlNET
                : _configService.Api.Urls.GachaUrlCN;
            var response = await _apiService.PostAsync<GachaRequest, GachaResponse<List<GachaData>>>(
                endpoint,
                req, cancellationToken: cancellationToken);

            if (response != null && response.Code == 0)
            {
                return (response.Data ?? []).Select(x => new GachaPull
                {
                    CardPoolType = x.CardPoolType, ResourceId = x.ResourceId, ResourceType = x.ResourceType,
                    Name = x.Name, Count = x.Count, Time = x.Time, QualityLevel = x.QualityLevel
                }).ToList();
            }

            throw new WwToolApiException("数据获取失败: " + (response?.Message ?? "未知错误"));
        }

        /// <summary>
        /// 获取账号信息
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        /// <exception cref="WwToolApiException"></exception>
        public async Task<GetUserInfoResponse?> GetUserInfoAsync(GetUserInfoRequest request, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.Debug($"获取用户信息 (登录类型: {request.LoginType}, 用户ID: {request.UserId})");
                var apiConf = _configService.Api;

                if (request.LoginType == 0)
                    request.LoginType = apiConf.FixedParams.LoginType;
                if (string.IsNullOrEmpty(request.Area))
                    request.Area = apiConf.FixedParams.Area;
                if (string.IsNullOrEmpty(request.Token))
                {
                    if (!string.IsNullOrEmpty(request.UserId))
                    {
                        _loginService.SwitchUserContext(request.UserId);
                    }
                    request.Token = _loginService.LoginContext.AccessToken;
                }

                string queryParams = $"?loginType={request.LoginType}" +
                                     $"&userId={request.UserId}" +
                                     $"&token={Uri.EscapeDataString(request.Token)}" +
                                     $"&area={request.Area}" +
                                     $"&userName={Uri.EscapeDataString(request.UserName)}";

                string url = apiConf.Urls.GetUserInfoUrl + queryParams;

                var response = await _apiService.GetAsync<GetUserInfoResponse>(url, cancellationToken: cancellationToken);
                if (response == null)
                    throw new WwToolApiException("查询账号大区信息请求未返回数据");
                return response;
            }
            catch (Exception ex) when (ex is not WwToolException and not OperationCanceledException)
            {
                throw new WwToolApiException("查询账号大区信息发生异常", ex);
            }
        }

        /// <summary>
        /// 查询玩家基本信息
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        /// <exception cref="WwToolApiException"></exception>
        public Task<QueryPlayerInfoResponse?> QueryPlayerInfoAsync(QueryPlayerInfoRequest request, CancellationToken cancellationToken = default) =>
            QueryWithRetryAsync(() => _apiService.PostAsync<QueryPlayerInfoRequest, QueryPlayerInfoResponse>(
                _configService.Api.Urls.QueryPlayerInfoUrl, request, cancellationToken: cancellationToken), x => x.Message, cancellationToken);

        public Task<QueryRoleResponse?> QueryRoleAsync(QueryRoleRequest request, CancellationToken cancellationToken = default) =>
            QueryWithRetryAsync(() => _apiService.PostAsync<QueryRoleRequest, QueryRoleResponse>(
                _configService.Api.Urls.QueryRoleUrl, request, cancellationToken: cancellationToken), x => x.Message, cancellationToken);

        /// <summary>只对读取请求的瞬态故障或服务端 retrying 信号做有限重试。</summary>
        private async Task<T?> QueryWithRetryAsync<T>(Func<Task<T?>> query, Func<T, string?> message, CancellationToken token) where T : class
        {
            int attempts = Math.Max(1, _configService.Api.MaxRetries);
            for (int attempt = 0; attempt < attempts; attempt++)
            {
                token.ThrowIfCancellationRequested();
                try
                {
                    T response = await query() ?? throw new WwToolApiException("查询未返回数据。");
                    if (message(response)?.Contains("retrying", StringComparison.OrdinalIgnoreCase) != true) return response;
                    if (attempt == attempts - 1) throw new WwToolApiException("查询重试次数已达上限。");
                }
                catch (Exception ex) when (!token.IsCancellationRequested &&
                    ex is System.Net.Http.HttpRequestException or TimeoutException or TaskCanceledException)
                {
                    if (attempt == attempts - 1) throw new WwToolApiException("查询远端数据失败。", ex);
                }
                await Task.Delay(Math.Max(0, _configService.Api.DelayMs), token);
            }
            throw new WwToolApiException("查询重试次数已达上限。");
        }
        /// <summary>
        /// 读取角色信息，并按需先从服务器同步。
        /// </summary>
        /// <param name="uid">UID</param>
        /// <param name="forceRefresh">是否从服务器同步数据</param>
        /// <returns></returns>
        /// <exception cref="WwToolDatabaseException"></exception>
        public async Task<PlayerSnapshot?> GetRoleDetailAsync(string uid, bool forceRefresh = false, CancellationToken cancellationToken = default)
        {
            if (forceRefresh)
            {
                var account = await _userRepository.GetUserAccountAsync(uid, cancellationToken);
                if (account != null && !string.IsNullOrEmpty(account.Region))
                {
                    await FetchAndSaveRoleDetailAsync(uid, account.Region, cancellationToken: cancellationToken);
                }
            }

            var roleDetali = await _playerInfoRepository.LoadPlayerRoleDataAsync(uid, cancellationToken);
            if (roleDetali == null)
            {
                throw new WwToolDatabaseException($"未找到对应 UID:{uid} 的角色数据");
            }
            return roleDetali;
        }

        /// <summary>
        /// 从服务器获取玩家基本信息并储存到本地数据库
        /// </summary>
        /// <param name="uid"></param>
        /// <param name="oauthCode">授权码</param>
        /// <returns></returns>
        /// <exception cref="WwToolAuthException"></exception>
        /// <exception cref="WwToolApiException"></exception>
        public async Task<PlayerRegionSummary?> FetchAndSavePlayerRegionInfoAsync(string? uid = null, string? oauthCode = null, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(oauthCode))
            {
                if (string.IsNullOrEmpty(uid)) throw new WwToolAuthException("缺少必要的授权信息，请重新登录");
                oauthCode = await _userRepository.GetOauthCodeAsync(uid, cancellationToken);
                if (string.IsNullOrEmpty(oauthCode)) throw new WwToolAuthException("授权码已过期或不存在，请重新登录");
            }

            var playerInfoResponse = await QueryPlayerInfoAsync(new QueryPlayerInfoRequest { OauthCode = oauthCode }, cancellationToken);
            if (playerInfoResponse == null || playerInfoResponse.Code != 0 || playerInfoResponse.Data == null || playerInfoResponse.Data.Count == 0)
            {
                throw new WwToolApiException(playerInfoResponse?.Message ?? "获取关联游戏角色信息失败");
            }

            string targetRegion = string.Empty;
            string targetRegionDataJson = string.Empty;

            foreach (var kv in playerInfoResponse.Data)
            {
                var candidate = System.Text.Json.JsonSerializer.Deserialize<PlayerRegionInfo>(kv.Value);
                if (candidate is null || (!string.IsNullOrEmpty(uid) && candidate.RoleId != uid)) continue;
                targetRegion = kv.Key;
                targetRegionDataJson = kv.Value;
                break;
            }

            if (string.IsNullOrEmpty(targetRegionDataJson))
                throw new WwToolAuthException("授权信息中没有当前账号，请重新登录该账号。");
            var playerRegion = PlayerSnapshotMapper.Map(System.Text.Json.JsonSerializer.Deserialize<PlayerRegionInfo>(targetRegionDataJson));
            if (playerRegion == null) throw new WwToolApiException("解析玩家角色信息失败");

            if (string.IsNullOrEmpty(uid)) _loginService.SwitchUserContext(playerRegion.RoleId);

            await _playerInfoRepository.SavePlayerRegionInfoAsync(playerRegion, targetRegion, oauthCode, cancellationToken);
            return playerRegion;
        }

        /// <summary>
        /// 从服务器获取角色详细信息并储存到本地数据库
        /// </summary>
        /// <param name="uid"></param>
        /// <param name="region"></param>
        /// <param name="oauthCode"></param>
        /// <returns></returns>
        /// <exception cref="WwToolAuthException"></exception>
        /// <exception cref="WwToolApiException"></exception>
        public async Task<PlayerSnapshot?> FetchAndSaveRoleDetailAsync(string uid, string region, string? oauthCode = null, CancellationToken cancellationToken = default)
        {
            _logger.Info($"获取并保存角色详情 (UID: {uid}, 大区: {region})");
            if (string.IsNullOrEmpty(oauthCode))
            {
                oauthCode = await _userRepository.GetOauthCodeAsync(uid, cancellationToken);
                if (string.IsNullOrEmpty(oauthCode)) throw new WwToolAuthException("本地授权已过期或不存在，请重新登录");
            }

            var request = new QueryRoleRequest
            {
                OauthCode = oauthCode,
                PlayerId = long.Parse(uid),
                Region = region
            };

            var roleResponse = await QueryRoleAsync(request, cancellationToken);
            if (roleResponse == null || roleResponse.Code != 0 || roleResponse.Data == null || roleResponse.Data.Count == 0)
            {
                throw new WwToolApiException(roleResponse?.Message ?? "获取游戏角色详细信息失败");
            }

            string? roleDetailJson = null;
            if (!roleResponse.Data.TryGetValue(region, out roleDetailJson))
            {
                foreach (var kv in roleResponse.Data)
                {
                    roleDetailJson = kv.Value;
                    break;
                }
            }

            if (string.IsNullOrWhiteSpace(roleDetailJson))
                throw new WwToolApiException("未找到玩家角色详情数据");

            var roleDetail = PlayerSnapshotMapper.Map(System.Text.Json.JsonSerializer.Deserialize<WwTool.Common.Models.ApiResponse.RoleDetailInfo>(roleDetailJson));
            if (roleDetail == null) throw new WwToolApiException("解析玩家角色详细信息失败");

            var account = await _userRepository.GetUserAccountAsync(uid, cancellationToken);
            var playerRegion = new PlayerRegionSummary
            {
                RoleId = uid,
                RoleName = account?.Name ?? "",
                Level = account?.Level ?? 0,
                Sex = account?.Sex ?? 0,
                HeadPhoto = account?.HeadPhoto ?? 0
            };

            await _playerInfoRepository.SavePlayerRoleDataAsync(uid, roleDetail, region, playerRegion, cancellationToken);
            return roleDetail;
        }

        /// <summary>
        /// 从服务器获取全量玩家信息并存储到数据库
        /// </summary>
        /// <param name="uid"></param>
        /// <param name="oauthCode"></param>
        /// <returns></returns>
        public async Task SyncAllUserDataAsync(string? uid = null, string? oauthCode = null, CancellationToken cancellationToken = default)
        {
            var regionInfo = await FetchAndSavePlayerRegionInfoAsync(uid, oauthCode, cancellationToken);
            if (regionInfo != null)
            {
                string region = "Default";
                var account = await _userRepository.GetUserAccountAsync(regionInfo.RoleId, cancellationToken);
                if (account != null && !string.IsNullOrEmpty(account.Region))
                {
                    region = account.Region;
                }

                await FetchAndSaveRoleDetailAsync(regionInfo.RoleId, region, oauthCode, cancellationToken);

                cancellationToken.ThrowIfCancellationRequested();
                _configService.User.LastUserId = regionInfo.RoleId;
                await _configService.SaveAllAsync();
            }
        }

    }
}
