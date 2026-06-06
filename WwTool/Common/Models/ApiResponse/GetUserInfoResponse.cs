using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace WwTool.Common.Models.ApiResponse
{
    /// <summary>
    /// 查询账号大区信息响应体
    /// </summary>
    public class GetUserInfoResponse
    {
        /// <summary>
        /// 状态码
        /// </summary>
        [JsonPropertyName("Code")]
        public int Code { get; set; }

        /// <summary>
        /// 用户 ID
        /// </summary>
        [JsonPropertyName("UserId")]
        public string UserId { get; set; } = string.Empty;

        /// <summary>
        /// SDK 登录码
        /// </summary>
        [JsonPropertyName("SdkLoginCode")]
        public int SdkLoginCode { get; set; }

        /// <summary>
        /// 角色账号列表
        /// </summary>
        [JsonPropertyName("UserInfos")]
        public List<object>? UserInfos { get; set; }
    }
}