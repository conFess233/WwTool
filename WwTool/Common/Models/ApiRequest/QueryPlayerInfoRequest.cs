using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace WwTool.Common.Models.ApiRequest
{
    /// <summary>
    /// 查询玩家信息请求体
    /// </summary>
    public class QueryPlayerInfoRequest
    {
        /// <summary>
        /// 授权码
        /// </summary>
        [JsonPropertyName("oauthCode")]
        public string OauthCode { get; set; } = string.Empty;
    }
}
