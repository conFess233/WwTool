using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using WwTool.Common.Models.ApiRequest;
using WwTool.Common.Models.ApiResponse;
using WwTool.Common.Models.Config;
using WwTool.Common.Utils;
using WwTool.Services.Interfaces;

namespace WwTool.Services
{
    /// <summary>
    /// 登录服务 (目前仅支持邮箱账号密码登录)
    /// </summary>
    public class LoginService : ILoginService
    {
        private readonly IHttpService _apiService;
        private readonly IConfigService _configService;
        private readonly ILoggerService _logger;
        private LoginContext _loginContext;
        private LoginContext _latestAuthenticatedContext;
        private readonly Dictionary<string, LoginContext> _userContexts = new();
        private string _currentUid = string.Empty;

        /// <summary>
        /// 登录过程的上下文，包含登录状态和相关数据，以便后续验证
        /// </summary>
        public LoginContext LoginContext => _loginContext;
        public LoginContext LatestAuthenticatedContext => _latestAuthenticatedContext;

        public LoginService(IHttpService apiService, IConfigService configService, ILoggerService logger)
        {
            _apiService = apiService;
            _configService = configService;
            _logger = logger;
            _loginContext = new LoginContext();
            _latestAuthenticatedContext = _loginContext;
        }

        /// <summary>
        /// 根据指定的 UID 切换内存中的登录上下文
        /// </summary>
        public void SwitchUserContext(string uid)
        {
            if (string.IsNullOrEmpty(uid)) return;

            LoginContext context;
            if (string.IsNullOrEmpty(_currentUid) && !string.IsNullOrEmpty(_loginContext.AccessToken))
            {
                // 新认证已由玩家信息接口确认 UID，替换该 UID 的旧会话。
                context = _loginContext;
                _userContexts[uid] = context;
            }
            else if (!_userContexts.TryGetValue(uid, out context!))
            {
                context = new LoginContext();
                _userContexts[uid] = context;
            }
            _loginContext = context;
            _currentUid = uid;
        }

        /// <summary>删除账号后立即清理该 UID 的内存认证数据。</summary>
        public void RemoveUserContext(string uid)
        {
            if (_userContexts.Remove(uid, out var removed) && ReferenceEquals(removed, _latestAuthenticatedContext))
                _latestAuthenticatedContext = new LoginContext();
            if (_currentUid == uid)
            {
                _currentUid = string.Empty;
                _loginContext = new LoginContext();
            }
        }
        /// <summary>
        /// 邮箱登录
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        public async Task<EmailLoginResponse?> EmailLoginAsync(EmailLoginRequest request, CancellationToken cancellationToken = default)
        {
            _logger.Info("尝试邮箱登录。");
            if (string.IsNullOrEmpty(request.Password) || string.IsNullOrEmpty(request.Email))
                throw new ArgumentException("Password 或 Email 不能为空");

            var apiConf = _configService.Api;
            request.ProductId = apiConf.FixedParams.LoginProductId;
            request.ProductKey = apiConf.FixedParams.ProductKey;
            request.ProjectId = apiConf.FixedParams.ProjectId;
            request.SdkVersion = apiConf.FixedParams.SdkVersion;
            request.RedirectUri = apiConf.FixedParams.RedirectUri;
            request.ChannelId = apiConf.FixedParams.EmailLoginChannelId;
            request.ClientId = apiConf.FixedParams.ClientId;
            request.Platform = apiConf.FixedParams.Platform;
            request.ResponseType = apiConf.FixedParams.ResponseType;
            request.__e__ = apiConf.FixedParams.DefaultE;

            request.DeviceNum = Crypto.GetDeviceNum();
            // 新登录不能修改旧 UID 已缓存的会话。
            _loginContext = new LoginContext { DeviceNum = request.DeviceNum };
            _currentUid = string.Empty;
            LoginContext context = _loginContext;
            request.Sign = Crypto.GenerateSignature(request.ToDictionary(), apiConf.FixedParams.ClientSecret);

            var response = await _apiService.PostFormAsync<EmailLoginResponse>(_configService.Api.Urls.EmailLoginUrl, request.ToDictionary(), cancellationToken: cancellationToken);

            if (response != null)
            {
                if (response.Codes == 0 && ReferenceEquals(context, _loginContext)) _latestAuthenticatedContext = context;
                if (response.Code != null)
                    context.Code = response.Code;
                if (!string.IsNullOrWhiteSpace(response.Cuid))
                    context.CUid = response.Cuid;
                if (!string.IsNullOrWhiteSpace(response.Username))
                    context.CName = response.Username;

                return response;
            }
            return null;
        }

        /// <summary>
        /// 自动登录
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        public async Task<AutoTokenResponse?> AutoTokenAsync(AutoTokenRequest request, CancellationToken cancellationToken = default)
        {
            LoginContext context = _loginContext;
            _logger.Info("尝试通过 Token 自动登录...");
            if (string.IsNullOrEmpty(request.Token))
                throw new ArgumentException("Token 不能为空");

            var apiConf = _configService.Api;
            request.ProductId = apiConf.FixedParams.LoginProductId;
            request.ProjectId = apiConf.FixedParams.ProjectId;
            request.SdkVersion = apiConf.FixedParams.SdkVersion;
            request.RedirectUri = apiConf.FixedParams.RedirectUri;
            request.ChannelId = apiConf.FixedParams.AutoLoginChannelId;
            request.ClientId = apiConf.FixedParams.ClientId;
            request.ResponseType = apiConf.FixedParams.ResponseType;

            if (string.IsNullOrEmpty(context.DeviceNum))
            {
                context.DeviceNum = Crypto.GetDeviceNum();
            }
            request.DeviceNum = context.DeviceNum;

            request.Sign = Crypto.GenerateSignature(request.ToDictionary(), apiConf.FixedParams.ClientSecret);

            var response = await _apiService.PostFormAsync<AutoTokenResponse>(_configService.Api.Urls.AutoLoginUrl, request.ToDictionary(), cancellationToken: cancellationToken);

            if (response != null)
            {
                if (response.Code != null)
                    context.Code = response.Code;

                return response;
            }
            return null;
        }

        /// <summary>
        /// 获取 OauthCode 授权码
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        public async Task<GenerateResponse?> GenerateAsync(GenerateRequest request, CancellationToken cancellationToken = default)
        {
            LoginContext context = _loginContext;
            _logger.Debug("正在生成 OauthCode...");
            var apiConf = _configService.Api;
            request.ClientId = apiConf.FixedParams.ClientId;
            request.Scope = apiConf.FixedParams.LauncherScope;
            request.ProductId = apiConf.FixedParams.AuthProductId;
            request.ClientSecret = apiConf.FixedParams.ClientSecret;
            request.ProjectId = apiConf.FixedParams.ProjectId;
            request.RedirectUri = apiConf.FixedParams.RedirectUri;

            if (string.IsNullOrEmpty(context.DeviceNum))
                throw new ArgumentException("DeviceNum 为空, 请先登录");
            request.DeviceNum = context.DeviceNum;

            if (string.IsNullOrEmpty(context.AccessToken))
                await GetTokenAsync(new GetTokenRequest(), cancellationToken);
            if (!ReferenceEquals(context, _loginContext)) throw new OperationCanceledException(cancellationToken);
            request.AccessToken = context.AccessToken;

            var response = await _apiService.PostFormAsync<GenerateResponse>(_configService.Api.Urls.GenerateUrl, request.ToDictionary(), cancellationToken: cancellationToken);

            if (response != null)
            {
                return response;
            }
            return null;
        }

        /// <summary>
        /// 获取 Token，用于Generate验证
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        public async Task<GetTokenResponse?> GetTokenAsync(GetTokenRequest request, CancellationToken cancellationToken = default)
        {
            LoginContext context = _loginContext;
            _logger.Debug("正在获取 AccessToken...");
            var apiConf = _configService.Api;
            request.ProductId = apiConf.FixedParams.AuthProductId;
            request.ProjectId = apiConf.FixedParams.ProjectId;
            request.ClientId = apiConf.FixedParams.ClientId;
            request.ClientSecret = apiConf.FixedParams.ClientSecret;
            request.GrantType = apiConf.FixedParams.GrantType;
            request.RedirectUri = apiConf.FixedParams.RedirectUri;

            if (string.IsNullOrEmpty(context.Code) || string.IsNullOrEmpty(context.DeviceNum))
                throw new ArgumentException("Code, DeviceNum 为空, 请先登录");

            request.DeviceNum = context.DeviceNum;
            request.Code = context.Code;

            request.Sign = Crypto.GenerateSignature(request.ToDictionary(), apiConf.FixedParams.ClientSecret);

            var response = await _apiService.PostFormAsync<GetTokenResponse>(_configService.Api.Urls.GetTokenUrl, request.ToDictionary(), cancellationToken: cancellationToken);

            if (response != null)
            {
                if (response.AccessToken != null)
                    context.AccessToken = response.AccessToken;

                return response;
            }

            return null;
        }


    }
}
