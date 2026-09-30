using System.IO;

namespace WwTool.Common.Models.Domain
{
    /// <summary>
    /// 独立于 API 和 EF 的抽卡读取模型，保持来源顺序
    /// </summary>
    public sealed class GachaPull
    {
        /// <summary>
        /// 卡池类型
        /// </summary>
        public string CardPoolType { get; set; } = string.Empty;
        /// <summary>
        /// 资源 ID
        /// </summary>
        public int ResourceId { get; set; }
        /// <summary>
        /// 资源类型
        /// </summary>
        public string ResourceType { get; set; } = string.Empty;
        /// <summary>
        /// 资源名称
        /// </summary>
        public string Name { get; set; } = string.Empty;
        /// <summary>
        /// 数量
        /// </summary>
        public int Count { get; set; }
        /// <summary>
        /// 获取时间
        /// </summary>
        public string Time { get; set; } = string.Empty;
        /// <summary>
        /// 品质
        /// </summary>
        public int QualityLevel { get; set; }

        public string IconPath
        {
            get
            {
                return $"Local/Icons/{ResourceId}.png";
            }
        }
    }
}
