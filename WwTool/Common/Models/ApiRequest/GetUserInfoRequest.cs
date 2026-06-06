using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace WwTool.Common.Models.ApiRequest
{
    /// <summary>
    /// 查询账号大区信息请求参数
    /// </summary>
    public class GetUserInfoRequest
    {
        /// <summary>
        /// 登录类型
        /// </summary>
        [JsonPropertyName("loginType")]
        public int LoginType { get; set; } = 0;
        
        /// <summary>
        /// SDK 用户 ID
        /// </summary>
        [JsonPropertyName("userId")]
        public string UserId { get; set; } = string.Empty;
        
        /// <summary>
        /// 访问令牌
        /// </summary>
        [JsonPropertyName("token")]
        public string Token { get; set; } = string.Empty;
        
        /// <summary>
        /// 地区参数
        /// </summary>
        [JsonPropertyName("area")]
        public string Area { get; set; } = string.Empty;
        
        /// <summary>
        /// 用户名
        /// </summary>
        [JsonPropertyName("userName")]
        public string UserName { get; set; } = string.Empty;
    }
}