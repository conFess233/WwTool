using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace WwTool.Common.Models.ApiResponse
{
    /// <summary>
    /// 查询玩家信息响应体
    /// </summary>
    public class QueryPlayerInfoResponse
    {
        /// <summary>
        /// 状态码
        /// </summary>
        [JsonPropertyName("code")]
        public int Code { get; set; }

        /// <summary>
        /// 提示信息
        /// </summary>
        [JsonPropertyName("message")]
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// 按大区组织的玩家信息映射
        /// </summary>
        [JsonPropertyName("data")]
        public Dictionary<string, string>? Data { get; set; }

        /// <summary>
        /// 时间戳
        /// </summary>
        [JsonPropertyName("timestamp")]
        public long Timestamp { get; set; }
    }

    /// <summary>
    /// 玩家大区基础信息
    /// </summary>
    public class PlayerRegionInfo
    {
        /// <summary>
        /// 玩家 UID
        /// </summary>
        [JsonPropertyName("roleId")]
        public string RoleId { get; set; } = string.Empty;

        /// <summary>
        /// 角色名
        /// </summary>
        [JsonPropertyName("roleName")]
        public string RoleName { get; set; } = string.Empty;

        /// <summary>
        /// 等级
        /// </summary>
        [JsonPropertyName("level")]
        public int Level { get; set; }

        /// <summary>
        /// 性别
        /// </summary>
        [JsonPropertyName("sex")]
        public int Sex { get; set; }

        /// <summary>
        /// 头像 ID
        /// </summary>
        [JsonPropertyName("headPhoto")]
        public int HeadPhoto { get; set; }

        public string IconPath => $"Local/Icons/{HeadPhoto}.png";
    }
}
