using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace WwTool.Common.Models.ApiResponse
{
    /// <summary>
    /// 邮箱密码登录响应体
    /// </summary>
    public class EmailLoginResponse
    {
        // 基础与风控字段

        /// <summary>
        /// 状态码，0 表示成功，41000 表示需要行为校验
        /// </summary>
        [JsonPropertyName("codes")]
        public int Codes { get; set; }

        /// <summary>
        /// 接口提示信息
        /// </summary>
        [JsonPropertyName("error_description")]
        public string? ErrorDescription { get; set; }

        /// <summary>
        /// 时间戳
        /// </summary>
        [JsonPropertyName("timestamp")]
        public long Timestamp { get; set; }



        // 登录成功后返回的详细字段
        
        /// <summary>
        /// 用户名
        /// </summary>
        [JsonPropertyName("username")]
        public string? Username { get; set; }

        /// <summary>
        /// SDK 用户 ID
        /// </summary>
        [JsonPropertyName("sdkuserid")]
        public string? SdkUserId { get; set; }

        /// <summary>
        /// 用户 ID
        /// </summary>
        [JsonPropertyName("id")]
        public long? Id { get; set; }

        /// <summary>
        /// 登录类型
        /// </summary>
        [JsonPropertyName("loginType")]
        public int? LoginType { get; set; }


        /// <summary>
        /// 后续换取访问令牌使用的授权码
        /// </summary>
        [JsonPropertyName("code")]
        public string? Code { get; set; }

        /// <summary>
        /// 临时 token
        /// </summary>
        [JsonPropertyName("temp_token")]
        public string? TempToken { get; set; }

        [JsonPropertyName("idStat")]
        public int? IdStat { get; set; }

        /// <summary>
        /// 用户类型
        /// </summary>
        [JsonPropertyName("userType")]
        public int? UserType { get; set; }

        /// <summary>
        /// 字符串形式用户 ID
        /// </summary>
        [JsonPropertyName("cuid")]
        public string? Cuid { get; set; }

        [JsonPropertyName("showPaw")]
        public bool? ShowPaw { get; set; }

        [JsonPropertyName("bindDevStat")]
        public int? BindDevStat { get; set; }

        [JsonPropertyName("bindDevSwitch")]
        public bool? BindDevSwitch { get; set; }


        /// <summary>
        /// 自动登录 token
        /// </summary>
        [JsonPropertyName("autoToken")]
        public string? AutoToken { get; set; }

        [JsonPropertyName("thirdNickName")]
        public string? ThirdNickName { get; set; }


        /// <summary>
        /// 是否首次登录，0 表示否
        /// </summary>
        [JsonPropertyName("firstLgn")]
        public int? FirstLgn { get; set; }


        [JsonPropertyName("email")]
        public string? Email { get; set; }


        [JsonPropertyName("bind")]
        public int? Bind { get; set; }

    }
}
