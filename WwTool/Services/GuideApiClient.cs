using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using WwTool.Common.Exceptions;
using WwTool.Common.Models.ApiRequest;
using WwTool.Common.Models.ApiResponse;
using WwTool.Services.Interfaces;

namespace WwTool.Services;

public sealed class GuideApiClient(
    IHttpClientFactory httpClientFactory,
    IConfigService configService,
    ILoggerService logger) : IGuideApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString
    };

    public async Task<string> LoginAsync(GuideLoginRequest request, string language, CancellationToken cancellationToken = default)
    {
        GuideEnvelope<GuideLoginData> response = await SendAsync<GuideLoginData>(HttpMethod.Post, "/user/login/sdk", request, null, language, cancellationToken);
        if (string.IsNullOrWhiteSpace(response.Data?.Token))
            throw new GuideAuthenticationRequiredException("Guide 登录未返回有效令牌。");
        return response.Data.Token;
    }

    public async Task<IReadOnlyList<GuidePlayer>> GetPlayersAsync(string token, string language, CancellationToken cancellationToken = default) =>
        (await SendAsync<List<GuidePlayer>>(HttpMethod.Get, "/user/player/list", null, token, language, cancellationToken)).Data ?? [];

    public async Task ChoosePlayerAsync(string token, string language, GuideChoosePlayerRequest request, CancellationToken cancellationToken = default) =>
        _ = await SendAsync<GuideChooseData>(HttpMethod.Post, "/user/player/choose", request, token, language, cancellationToken);

    public async Task<IReadOnlyList<GuideAvatar>> GetAvatarsAsync(string token, string language, CancellationToken cancellationToken = default) =>
        (await SendAsync<List<GuideAvatar>>(HttpMethod.Get, "/role/avatar/list", null, token, language, cancellationToken)).Data ?? [];

    public async Task<IReadOnlyList<GuideIntroductionSummary>> GetIntroductionsAsync(string token, string language, string roleGbId, CancellationToken cancellationToken = default) =>
        (await SendAsync<List<GuideIntroductionSummary>>(HttpMethod.Get, $"/introduction/list?roleGbId={Uri.EscapeDataString(roleGbId)}", null, token, language, cancellationToken)).Data ?? [];

    public async Task<GuideIntroductionDetail?> GetIntroductionAsync(string token, string language, string roleGbId, long id, CancellationToken cancellationToken = default) =>
        (await SendAsync<GuideIntroductionDetail>(HttpMethod.Get, $"/introduction/info?roleGbId={Uri.EscapeDataString(roleGbId)}&id={id}", null, token, language, cancellationToken)).Data;

    private async Task<GuideEnvelope<T>> SendAsync<T>(HttpMethod method, string path, object? body, string? token, string language, CancellationToken cancellationToken)
    {
        string[] bases = [configService.Api.Urls.GuideBaseUrl, configService.Api.Urls.GuideFallbackBaseUrl];
        Exception? lastError = null;
        for (int attempt = 0; attempt < bases.Length; attempt++)
        {
            try
            {
                using var request = new HttpRequestMessage(method, bases[attempt].TrimEnd('/') + path);
                request.Headers.TryAddWithoutValidation("x-language", language);
                request.Headers.TryAddWithoutValidation("Accept-Language", language);
                request.Headers.TryAddWithoutValidation("User-Agent", configService.Api.CommonHeaders.UserAgent);
                if (!string.IsNullOrWhiteSpace(token))
                    request.Headers.TryAddWithoutValidation("x-token", token);
                if (body is not null)
                    request.Content = JsonContent.Create(body, options: JsonOptions);

                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeout.CancelAfter(TimeSpan.FromSeconds(configService.Api.TimeoutSeconds));
                HttpClient client = httpClientFactory.CreateClient("WwToolClient");
                using HttpResponseMessage response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
                if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
                    throw new GuideAuthenticationRequiredException("Guide 登录状态已失效，请重新登录。");
                if ((int)response.StatusCode >= 500 && attempt + 1 < bases.Length)
                {
                    logger.Warn($"Guide API 主域名返回 {(int)response.StatusCode}，正在使用备用域名：{method} {path}");
                    continue;
                }
                if (!response.IsSuccessStatusCode)
                    throw new GuideApiException($"Guide API 请求失败：HTTP {(int)response.StatusCode} ({method} {path})");

                await using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                GuideEnvelope<T>? envelope = await JsonSerializer.DeserializeAsync<GuideEnvelope<T>>(stream, JsonOptions, cancellationToken);
                if (envelope is null)
                    throw new GuideApiException($"Guide API 返回了无法解析的响应 ({method} {path})");
                if (envelope.Code is 401 or 403)
                    throw new GuideAuthenticationRequiredException("Guide 登录状态已失效，请重新登录。");
                if (envelope.Code != 200)
                    throw new GuideApiException($"Guide API 返回错误：{envelope.Code} {envelope.Message}");
                return envelope;
            }
            catch (GuideAuthenticationRequiredException)
            {
                throw;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex) when (attempt + 1 < bases.Length && (ex is HttpRequestException || ex is OperationCanceledException))
            {
                lastError = ex;
                logger.Warn($"Guide API 主域名连接失败，正在使用备用域名：{method} {path}", ex);
            }
            catch (GuideApiException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new GuideApiException($"Guide API 请求失败 ({method} {path})", ex);
            }
        }
        throw new GuideApiException($"Guide API 请求失败 ({method} {path})", lastError ?? new HttpRequestException());
    }
}
