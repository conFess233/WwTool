using WwTool.Common.Models.ApiRequest;
using WwTool.Common.Models.ApiResponse;

namespace WwTool.Services.Interfaces
{
    public class LoginContext
    {
        public string DeviceNum { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public string AccessToken { get; set; } = string.Empty;
    }

    public interface ILoginService
    {
        LoginContext LoginContext { get; }

        void SwitchUserContext(string uid);

        Task<EmailLoginResponse?> EmailLoginAsync(EmailLoginRequest request);
        Task<GenerateResponse?> GenerateAsync(GenerateRequest request);

        Task<GetTokenResponse?> GetTokenAsync(GetTokenRequest request);

        Task<AutoTokenResponse?> AutoTokenAsync(AutoTokenRequest request);
    }
}
