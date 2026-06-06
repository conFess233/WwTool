using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace WwTool.Common.Models.ApiResponse
{
    /// <summary>
    /// 自动登录响应体
    /// </summary>
    public class AutoTokenResponse
    {
        /// <summary>
        /// 状态码
        /// </summary>
        [JsonPropertyName("codes")]
        public int Codes { get; set; } = 0;

        /// <summary>
        /// 授权码
        /// </summary>
        [JsonPropertyName("code")]
        public string Code { get; set; } = string.Empty;

        /// <summary>
        /// 临时 token
        /// </summary>
        [JsonPropertyName("tempToken")]
        public string TempToken { get; set; } = string.Empty;

        /// <summary>
        /// 自动登录凭证
        /// </summary>
        [JsonPropertyName("autoToken")]
        public string AutoToken { get; set; } = string.Empty;

        /// <summary>
        /// 登录邮箱
        /// </summary>
        [JsonPropertyName("email")]
        public string Email { get; set; } = string.Empty;
    }
}