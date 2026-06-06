using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace WwTool.Common.Models.ApiRequest
{
    /// <summary>
    /// 查询玩家角色详情请求体
    /// </summary>
    public class QueryRoleRequest
    {
        /// <summary>
        /// 启动器授权码
        /// </summary>
        [JsonPropertyName("oauthCode")]
        public string OauthCode { get; set; } = string.Empty;
        
        /// <summary>
        /// 玩家游戏 UID
        /// </summary>
        [JsonPropertyName("playerId")]
        public long PlayerId { get; set; }
        
        /// <summary>
        /// 大区标识
        /// </summary>
        [JsonPropertyName("region")]
        public string Region { get; set; } = string.Empty;
    }
}