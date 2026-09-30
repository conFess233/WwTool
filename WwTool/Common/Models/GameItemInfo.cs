using System.Text.Json.Serialization;

namespace WwTool.Common.Models
{
    /// <summary>
    /// 游戏物品资源信息模型
    /// </summary>
    public class GameItemInfo
    {
        [JsonIgnore]
        public int ResourceId { get; set; }

        [JsonPropertyName("type")]
        public string Type { get; set; } = "";

        [JsonPropertyName("qualityLevel")]
        public int qualityLevel { get; set; }

        [JsonPropertyName("isLimited")]
        public bool? IsLimited { get; set; }

        [JsonPropertyName("iconUrl")]
        public string? IconUrl { get; set; }

        [JsonPropertyName("portraitUrl")]
        public string? PortraitUrl { get; set; }

        [JsonPropertyName("names")]
        public Dictionary<string, string> NameDict { get; set; } = new();

        /// <summary>
        /// 获取当前语言下的物品名称
        /// </summary>
        public string GetName(string langCode = "zh-Hans")
        {
            if (NameDict.TryGetValue(langCode, out var name) && !string.IsNullOrWhiteSpace(name))
                return name;

            // 缺省回退机制
            return NameDict.TryGetValue("zh-Hans", out var defaultName) && !string.IsNullOrWhiteSpace(defaultName) ? defaultName : string.Empty;
        }
    }
}
