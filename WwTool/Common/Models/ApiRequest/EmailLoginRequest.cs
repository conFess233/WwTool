using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;
using WwTool.Services;

namespace WwTool.Common.Models.ApiRequest
{
    /// <summary>
    /// 邮箱密码登录请求体
    /// </summary>
    public class EmailLoginRequest
    {
        // 必填与固定参数
        /// <summary>
        /// 固定参数
        /// </summary>
        [JsonPropertyName("__e__")]
        public string __e__ { get; set; } = string.Empty;
        
        /// <summary>
        /// 登录邮箱
        /// </summary>
        [JsonPropertyName("email")]
        public string Email { get; set; } = string.Empty;
        /// <summary>
        /// 客户端标识 (固定值)
        /// </summary>
        [JsonPropertyName("client_id")]
        public string ClientId { get; set; } = string.Empty;
        
        /// <summary>
        /// 设备 ID，任意大写 UUIDv4
        /// </summary>
        [JsonPropertyName("deviceNum")]
        public string DeviceNum { get; set; } = string.Empty;
        
        /// <summary>
        /// 加密后的密码串
        /// </summary>
        [JsonPropertyName("password")]
        public string Password { get; set; } = string.Empty;
        [JsonPropertyName("platform")]
        public string Platform { get; set; } = string.Empty;
        /// <summary>
        /// 产品 ID (固定值)
        /// </summary>
        [JsonPropertyName("productId")]
        public string ProductId { get; set; } = string.Empty;
        
        /// <summary>
        /// 产品 Key (固定值)
        /// </summary>
        [JsonPropertyName("productKey")]
        public string ProductKey { get; set; } = string.Empty;
        
        /// <summary>
        /// 项目 ID (固定值)
        /// </summary>
        [JsonPropertyName("projectId")]
        public string ProjectId { get; set; } = string.Empty;
        
        /// <summary>
        /// 重定向 URI (固定值)
        /// </summary>
        [JsonPropertyName("redirect_uri")]
        public string RedirectUri { get; set; } = string.Empty;
        
        /// <summary>
        /// 登录后换取授权码 (固定值)
        /// </summary>
        [JsonPropertyName("response_type")]
        public string ResponseType { get; set; } = string.Empty;
        
        /// <summary>
        /// SDK 版本 (固定值)
        /// </summary>
        [JsonPropertyName("sdkVersion")]
        public string SdkVersion { get; set; } = string.Empty;
        
        /// <summary>
        /// 登录渠道 (固定值)
        /// </summary>
        [JsonPropertyName("channelId")]
        public string ChannelId { get; set; } = string.Empty;
        
        /// <summary>
        /// 签名，对所有参数排序后加 secret_key 进行 MD5 加密
        /// </summary>
        [JsonPropertyName("sign")]
        public string Sign { get; set; } = string.Empty;

        // 极验风控参数（触发风控后必填）
        [JsonPropertyName("geetestCaptchaOutput")]
        public string? GeetestCaptchaOutput { get; set; }
        [JsonPropertyName("geetestGenTime")]
        public string? GeetestGenTime { get; set; }
        [JsonPropertyName("geetestLotNumber")]
        public string? GeetestLotNumber { get; set; }
        [JsonPropertyName("geetestPassToken")]
        public string? GeetestPassToken { get; set; }


        /// <summary>
        /// 转换为字典以供 application/x-www-form-urlencoded 提交
        /// </summary>
        public Dictionary<string, string> ToDictionary()
        {
            var dict = new Dictionary<string, string>
            {
                { "__e__", __e__ },
                { "email", Email },
                { "client_id", ClientId },
                { "deviceNum", DeviceNum },
                { "password", Password },
                { "platform", Platform },
                { "productId", ProductId },
                { "productKey", ProductKey },
                { "projectId", ProjectId },
                { "redirect_uri", RedirectUri },
                { "response_type", ResponseType },
                { "sdkVersion", SdkVersion },
                { "channelId", ChannelId },
                { "sign", Sign }
            };

            // 仅在赋值了风控参数时加入字典
            if (!string.IsNullOrEmpty(GeetestCaptchaOutput))
                dict.Add("geetestCaptchaOutput", GeetestCaptchaOutput);

            if (!string.IsNullOrEmpty(GeetestGenTime))
                dict.Add("geetestGenTime", GeetestGenTime);

            if (!string.IsNullOrEmpty(GeetestLotNumber))
                dict.Add("geetestLotNumber", GeetestLotNumber);

            if (!string.IsNullOrEmpty(GeetestPassToken))
                dict.Add("geetestPassToken", GeetestPassToken);

            return dict;
        }
    }
}
