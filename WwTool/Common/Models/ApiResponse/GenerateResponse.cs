using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace WwTool.Common.Models.ApiResponse
{
    /// <summary>
    /// 生成授权码响应体
    /// </summary>
    public class GenerateResponse
    {
        /// <summary>
        /// 启动器授权码
        /// </summary>
        [JsonPropertyName("oauthCode")]
        public string OauthCode { get; set; } = string.Empty;

        /// <summary>
        /// 状态码
        /// </summary>
        [JsonPropertyName("codes")]
        public int Codes { get; set; } = -1;

        /// <summary>
        /// 错误描述
        /// </summary>
        [JsonPropertyName("error_description")]
        public string ErrorDescription { get; set; } = string.Empty;

        /// <summary>
        /// 时间戳
        /// </summary>
        [JsonPropertyName("timestamp")]
        public long TimeStamp { get; set; } = 0;
    }
}