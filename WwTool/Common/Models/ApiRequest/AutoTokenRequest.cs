using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace WwTool.Common.Models.ApiRequest
{
    /// <summary>
    /// 自动登录请求参数
    /// </summary>
    public class AutoTokenRequest
    {
        /// <summary>
        /// 自动登录 token
        /// </summary>
        [JsonPropertyName("token")]
        public string Token { get; set; } = string.Empty;
        
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
        /// SDK 版本
        /// </summary>
        [JsonPropertyName("sdkVersion")]
        public string SdkVersion { get; set; } = string.Empty;
        
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
        /// 重定向 URI
        /// </summary>
        [JsonPropertyName("redirect_uri")]
        public string RedirectUri { get; set; } = string.Empty;
        
        /// <summary>
        /// 响应类型
        /// </summary>
        [JsonPropertyName("response_type")]
        public string ResponseType { get; set; } = string.Empty;
        
        /// <summary>
        /// 渠道 ID
        /// </summary>
        [JsonPropertyName("channelId")]
        public string ChannelId { get; set; } = string.Empty;
        
        /// <summary>
        /// 参数签名
        /// </summary>
        [JsonPropertyName("sign")]
        public string Sign { get; set; } = string.Empty;

        /// <summary>
        /// 转换为字典以供 application/x-www-form-urlencoded 提交
        /// </summary>
        public Dictionary<string, string> ToDictionary()
        {
            return new Dictionary<string, string>
            {
                { "token", Token },
                { "client_id", ClientId },
                { "deviceNum", DeviceNum },
                { "sdkVersion", SdkVersion },
                { "productId", ProductId },
                { "projectId", ProjectId },
                { "redirect_uri", RedirectUri },
                { "response_type", ResponseType },
                { "channelId", ChannelId },
                { "sign", Sign }
            };
        }
    }
}