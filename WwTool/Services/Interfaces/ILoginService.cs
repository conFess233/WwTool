using WwTool.Common.Models.ApiRequest;
using WwTool.Common.Models.ApiResponse;

namespace WwTool.Services.Interfaces
{
    public class LoginContext
    {
        public string DeviceNum { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public string AccessToken { get; set; } = string.Empty;
        public string CUid { get; set; } = string.Empty;
        public string CName { get; set; } = string.Empty;
    }

    public interface ILoginService
    {
        LoginContext LoginContext { get; }
        LoginContext LatestAuthenticatedContext { get; }

        void SwitchUserContext(string uid);
        void RemoveUserContext(string uid);

        Task<EmailLoginResponse?> EmailLoginAsync(EmailLoginRequest request, CancellationToken cancellationToken = default);
        Task<GenerateResponse?> GenerateAsync(GenerateRequest request, CancellationToken cancellationToken = default);

        Task<GetTokenResponse?> GetTokenAsync(GetTokenRequest request, CancellationToken cancellationToken = default);

        Task<AutoTokenResponse?> AutoTokenAsync(AutoTokenRequest request, CancellationToken cancellationToken = default);
    }
}
