using WwTool.Common.Models.ApiRequest;
using WwTool.Common.Models.ApiResponse;

namespace WwTool.Services.Interfaces;

public interface IGuideApiClient
{
    Task<string> LoginAsync(GuideLoginRequest request, string language, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<GuidePlayer>> GetPlayersAsync(string token, string language, CancellationToken cancellationToken = default);
    Task ChoosePlayerAsync(string token, string language, GuideChoosePlayerRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<GuideAvatar>> GetAvatarsAsync(string token, string language, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<GuideIntroductionSummary>> GetIntroductionsAsync(string token, string language, string roleGbId, CancellationToken cancellationToken = default);
    Task<GuideIntroductionDetail?> GetIntroductionAsync(string token, string language, string roleGbId, long id, CancellationToken cancellationToken = default);
}
