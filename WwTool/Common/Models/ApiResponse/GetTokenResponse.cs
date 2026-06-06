using System.Text.Json.Serialization;

namespace WwTool.Common.Models.ApiResponse
{
    /// <summary>
    /// 获取访问令牌响应体
    /// </summary>
    public class GetTokenResponse
    {
        /// <summary>
        /// 状态码
        /// </summary>
        [JsonPropertyName("code")]
        public int Codes { get; set; }

        /// <summary>
        /// 访问令牌
        /// </summary>
        [JsonPropertyName("access_token")]
        public string AccessToken { get; set; }

        /// <summary>
        /// 过期时间
        /// </summary>
        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; set; }
    }
}