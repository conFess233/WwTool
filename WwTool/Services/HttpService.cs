using System.Net.Http;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using WwTool.Services.Interfaces;

namespace WwTool.Services
{
    /// <summary>
    /// HTTP 服务
    /// </summary>
    public class HttpService : IHttpService
    {
        private readonly IHttpClientFactory _factory;
        private readonly IConfigService _configService;
        private readonly ILoggerService _logger;

        public HttpService(IHttpClientFactory httpClientFactory, IConfigService configService, ILoggerService logger)
        {
            _factory = httpClientFactory;
            _configService = configService;
            _logger = logger;
        }

        private readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };
        private readonly JsonSerializerOptions _camelCaseOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

        /// <summary>
        /// GET 请求
        /// </summary>
        public async Task<T?> GetAsync<T>(string url, Dictionary<string, string>? dynamicHeaders = null, CancellationToken cancellationToken = default)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            return await SendAsync<T>(request, dynamicHeaders, null, cancellationToken);
        }

        /// <summary>
        /// POST JSON 请求 (application/json)
        /// </summary>
        public async Task<TResponse?> PostAsync<TRequest, TResponse>(string url, TRequest data, Dictionary<string, string>? dynamicHeaders = null, CancellationToken cancellationToken = default)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, url);
            string json = JsonSerializer.Serialize(data, _camelCaseOptions);
            request.Content = new StringContent(json, Encoding.UTF8, _configService.Api.CommonHeaders.DefaultContentType);
            return await SendAsync<TResponse>(request, dynamicHeaders, "POST", cancellationToken);
        }

        /// <summary>
        /// POST 表单请求 (application/x-www-form-urlencoded)
        /// </summary>
        public async Task<TResponse?> PostFormAsync<TResponse>(string url, Dictionary<string, string> formData, Dictionary<string, string>? dynamicHeaders = null, CancellationToken cancellationToken = default)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Content = new FormUrlEncodedContent(formData);
            return await SendAsync<TResponse>(request, dynamicHeaders, "POST FORM", cancellationToken);
        }

        /// <summary>统一请求头、超时、取消、响应校验和反序列化；请求内容仍由各入口构建。</summary>
        private async Task<T?> SendAsync<T>(HttpRequestMessage request,
            Dictionary<string, string>? dynamicHeaders, string? logCategory, CancellationToken cancellationToken)
        {
            using var client = CreateClient();
            ApplyHeaders(request, dynamicHeaders);
            if (logCategory is not null)
                _logger.Debug($"HTTP {logCategory} 请求: {GetEndpointCategory(request.RequestUri?.OriginalString ?? string.Empty)}");
            long startTime = Stopwatch.GetTimestamp();
            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(_configService.Api.TimeoutSeconds));
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
            using var response = await client.SendAsync(request, cts.Token).ConfigureAwait(false);
            if (logCategory is not null)
                _logger.Debug($"HTTP {logCategory} 响应: {(int)response.StatusCode} (耗时: {Stopwatch.GetElapsedTime(startTime).TotalMilliseconds}ms)");
            response.EnsureSuccessStatusCode();
            string result = await response.Content.ReadAsStringAsync(cts.Token).ConfigureAwait(false);
            return JsonSerializer.Deserialize<T>(result, _jsonOptions);
        }

        /// <summary>
        /// 添加请求头
        /// </summary>
        private void ApplyHeaders(HttpRequestMessage request, Dictionary<string, string>? dynamicHeaders)
        {
            var headers = _configService.Api.CommonHeaders;

            // 添加通用请求头
            request.Headers.TryAddWithoutValidation("User-Agent", headers.UserAgent);
            request.Headers.TryAddWithoutValidation("Accept-Language", headers.AcceptLanguage);
            request.Headers.TryAddWithoutValidation("Accept-Encoding", headers.AcceptEncoding);

            // 添加动态请求头
            if (dynamicHeaders != null)
            {
                foreach (var header in dynamicHeaders)
                {
                    request.Headers.TryAddWithoutValidation(header.Key, header.Value);
                }
            }
        }

        private HttpClient CreateClient()
        {
            return _factory.CreateClient("WwToolClient");
        }

        private static string GetEndpointCategory(string url) =>
            Uri.TryCreate(url, UriKind.Absolute, out Uri? uri) ? $"{uri.Host}{uri.AbsolutePath}" : "invalid-endpoint";
    }
}
