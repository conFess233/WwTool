using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace WwTool.Common.Models.ApiRequest
{
    /// <summary>
    /// 抽卡记录查询请求体
    /// </summary>
    public class GachaRequest
    {
        /// <summary>
        /// 玩家ID
        /// </summary>
        [JsonPropertyName("playerId")]
        public string PlayerId { get; set; } = string.Empty;
        
        //public string CardPoolId { get; set; }
        
        /// <summary>
        /// 服务器ID
        /// </summary>
        [JsonPropertyName("serverId")]
        public string ServerId { get; set; } = string.Empty;
        
        /// <summary>
        /// 语言代码 (例如 zh-Hans)
        /// </summary>
        [JsonPropertyName("languageCode")]
        public string LanguageCode { get; set; } = string.Empty;
        
        /// <summary>
        /// 抽卡记录ID，每个账号唯一，需在日志中获取
        /// </summary>
        [JsonPropertyName("recordId")]
        public string RecordId { get; set; } = string.Empty;
        
        /// <summary>
        /// 卡池类型
        /// </summary>
        [JsonPropertyName("cardPoolType")]
        public int CardPoolType { get; set; } = 0;
    }
}
