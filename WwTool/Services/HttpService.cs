using System.Net.Http;
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
        public async Task<T?> GetAsync<T>(string url, Dictionary<string, string>? dynamicHeaders = null)
        {
            using var client = CreateClient();
            using var request = new HttpRequestMessage(HttpMethod.Get, url);

            ApplyHeaders(request, dynamicHeaders);

            using var response = await client.SendAsync(request);
            response.EnsureSuccessStatusCode();

            string json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<T>(json, _jsonOptions);
        }

        /// <summary>
        /// POST JSON 请求 (application/json)
        /// </summary>
        public async Task<TResponse?> PostAsync<TRequest, TResponse>(string url, TRequest data, Dictionary<string, string>? dynamicHeaders = null)
        {
            using var client = CreateClient();
            using var request = new HttpRequestMessage(HttpMethod.Post, url);

            ApplyHeaders(request, dynamicHeaders);

            string json = JsonSerializer.Serialize(data, _camelCaseOptions);
            request.Content = new StringContent(json, Encoding.UTF8, _configService.Api.CommonHeaders.DefaultContentType);

            _logger.Debug($"HTTP POST 请求: {url}");
            var startTime = DateTime.Now;
            using var response = await client.SendAsync(request);
            var duration = (DateTime.Now - startTime).TotalMilliseconds;
            _logger.Debug($"HTTP POST 响应: {(int)response.StatusCode} (耗时: {duration}ms)");

            response.EnsureSuccessStatusCode();

            string result = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<TResponse>(result, _jsonOptions);
        }

        /// <summary>
        /// POST 表单请求 (application/x-www-form-urlencoded)
        /// </summary>
        public async Task<TResponse?> PostFormAsync<TResponse>(string url, Dictionary<string, string> formData, Dictionary<string, string>? dynamicHeaders = null)
        {
            using var client = CreateClient();
            using var request = new HttpRequestMessage(HttpMethod.Post, url);

            ApplyHeaders(request, dynamicHeaders);

            request.Content = new FormUrlEncodedContent(formData);

            _logger.Debug($"HTTP POST FORM 请求: {url}");
            var startTime = DateTime.Now;
            using var response = await client.SendAsync(request);
            var duration = (DateTime.Now - startTime).TotalMilliseconds;
            _logger.Debug($"HTTP POST FORM 响应: {(int)response.StatusCode} (耗时: {duration}ms)");

            response.EnsureSuccessStatusCode();

            string result = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<TResponse>(result, _jsonOptions);
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
            var client = _factory.CreateClient("WwToolClient");
            client.Timeout = TimeSpan.FromSeconds(_configService.Api.TimeoutSeconds);
            return client;
        }
    }
}
