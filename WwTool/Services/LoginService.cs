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

        /// <summary>
        /// 登录过程的上下文，包含登录状态和相关数据，以便后续验证
        /// </summary>
        public LoginContext LoginContext => _loginContext;

        public LoginService(IHttpService apiService, IConfigService configService, ILoggerService logger)
        {
            _apiService = apiService;
            _configService = configService;
            _logger = logger;
            _loginContext = new LoginContext();
        }

        /// <summary>
        /// 邮箱登录
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        public async Task<EmailLoginResponse?> EmailLoginAsync(EmailLoginRequest request)
        {
            _logger.Info($"尝试邮箱登录: {request.Email}");
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
            _loginContext.DeviceNum = request.DeviceNum;
            request.Sign = Crypto.GenerateSignature(request.ToDictionary(), apiConf.FixedParams.ClientSecret);

            var response = await _apiService.PostFormAsync<EmailLoginResponse>(_configService.Api.Urls.EmailLoginUrl, request.ToDictionary());

            if (response != null)
            {
                if (response.Code != null)
                    _loginContext.Code = response.Code;

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
        public async Task<AutoTokenResponse?> AutoTokenAsync(AutoTokenRequest request)
        {
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

            if (string.IsNullOrEmpty(_loginContext.DeviceNum))
            {
                _loginContext.DeviceNum = Crypto.GetDeviceNum();
            }
            request.DeviceNum = _loginContext.DeviceNum;

            request.Sign = Crypto.GenerateSignature(request.ToDictionary(), apiConf.FixedParams.ClientSecret);

            var response = await _apiService.PostFormAsync<AutoTokenResponse>(_configService.Api.Urls.AutoLoginUrl, request.ToDictionary());

            if (response != null)
            {
                if (response.Code != null)
                    _loginContext.Code = response.Code;

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
        public async Task<GenerateResponse?> GenerateAsync(GenerateRequest request)
        {
            _logger.Debug("正在生成 OauthCode...");
            var apiConf = _configService.Api;
            request.ClientId = apiConf.FixedParams.ClientId;
            request.Scope = apiConf.FixedParams.LauncherScope;
            request.ProductId = apiConf.FixedParams.AuthProductId;
            request.ClientSecret = apiConf.FixedParams.ClientSecret;
            request.ProjectId = apiConf.FixedParams.ProjectId;
            request.RedirectUri = apiConf.FixedParams.RedirectUri;

            if (string.IsNullOrEmpty(_loginContext.DeviceNum))
                throw new ArgumentException("DeviceNum 为空, 请先登录");
            request.DeviceNum = _loginContext.DeviceNum;

            if (string.IsNullOrEmpty(_loginContext.AccessToken))
                await GetTokenAsync(new GetTokenRequest());
            request.AccessToken = _loginContext.AccessToken;

            var response = await _apiService.PostFormAsync<GenerateResponse>(_configService.Api.Urls.GenerateUrl, request.ToDictionary());

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
        public async Task<GetTokenResponse?> GetTokenAsync(GetTokenRequest request)
        {
            _logger.Debug("正在获取 AccessToken...");
            var apiConf = _configService.Api;
            request.ProductId = apiConf.FixedParams.AuthProductId;
            request.ProjectId = apiConf.FixedParams.ProjectId;
            request.ClientId = apiConf.FixedParams.ClientId;
            request.ClientSecret = apiConf.FixedParams.ClientSecret;
            request.GrantType = apiConf.FixedParams.GrantType;
            request.RedirectUri = apiConf.FixedParams.RedirectUri;

            if (string.IsNullOrEmpty(_loginContext.Code) || string.IsNullOrEmpty(_loginContext.DeviceNum))
                throw new ArgumentException("Code, DeviceNum 为空, 请先登录");

            request.DeviceNum = _loginContext.DeviceNum;
            request.Code = _loginContext.Code;

            request.Sign = Crypto.GenerateSignature(request.ToDictionary(), apiConf.FixedParams.ClientSecret);

            var response = await _apiService.PostFormAsync<GetTokenResponse>(_configService.Api.Urls.GetTokenUrl, request.ToDictionary());

            if (response != null)
            {
                if (response.AccessToken != null)
                    _loginContext.AccessToken = response.AccessToken;

                return response;
            }

            return null;
        }


    }
}
