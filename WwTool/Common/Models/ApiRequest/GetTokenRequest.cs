using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Channels;

namespace WwTool.Common.Models.ApiRequest
{
    /// <summary>
    /// 获取访问令牌请求参数
    /// </summary>
    public class GetTokenRequest
    {
        /// <summary>
        /// 客户端标识
        /// </summary>
        [JsonPropertyName("client_id")]
        public string ClientId { get; set; } = string.Empty;

        /// <summary>
        /// 设备 ID
        /// </summary>
        [JsonPropertyName("deviceNum")]
        public string DeviceNum { get; set; } = string.Empty;

        /// <summary>
        /// 客户端密钥
        /// </summary>
        [JsonPropertyName("client_secret")]
        public string ClientSecret { get; set; } = string.Empty;

        /// <summary>
        /// 授权码
        /// </summary>
        [JsonPropertyName("code")]
        public string Code { get; set; } = string.Empty;

        /// <summary>
        /// 产品 ID
        /// </summary>
        [JsonPropertyName("productId")]
        public string ProductId { get; set; } = string.Empty;

        /// <summary>
        /// 项目 ID
        /// </summary>
        [JsonPropertyName("projectId")]
        public string ProjectId { get; set; } = string.Empty;

        /// <summary>
        /// 授权类型
        /// </summary>
        [JsonPropertyName("grant_type")]
        public string GrantType { get; set; } = string.Empty;

        /// <summary>
        /// 签名
        /// </summary>
        [JsonPropertyName("sign")]
        public string Sign { get; set; } = string.Empty;

        /// <summary>
        /// 重定向 URI
        /// </summary>
        [JsonPropertyName("redirect_uri")]
        public string RedirectUri { get; set; } = string.Empty;

        /// <summary>
        /// 转换为字典以供 application/x-www-form-urlencoded 提交
        /// </summary>
        public Dictionary<string, string> ToDictionary()
        {
            var dict = new Dictionary<string, string>
            {
                { "client_id", ClientId },
                { "deviceNum", DeviceNum },
                { "productId", ProductId },
                { "projectId", ProjectId },
                { "grant_type", GrantType },
                { "code", Code },
                { "client_secret", ClientSecret },
                { "redirect_uri", RedirectUri },
                { "sign", Sign }
            };
            return dict;
        }
    }
}
