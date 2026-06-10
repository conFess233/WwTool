using System.IO;

namespace WwTool.Common.Models.ApiResponse
{
    /// <summary>
    /// 单条抽卡记录数据
    /// </summary>
    public class GachaData
    {
        /// <summary>
        /// 卡池类型
        /// </summary>
        public string CardPoolType { get; set; }
        /// <summary>
        /// 资源 ID 
        /// </summary>
        public int ResourceId { get; set; }
        /// <summary>
        /// 资源类型 
        /// </summary>
        public string ResourceType { get; set; }
        /// <summary>
        /// 资源名称
        /// </summary>
        public string Name { get; set; }
        /// <summary>
        /// 数量
        /// </summary>
        public int Count { get; set; }
        /// <summary>
        /// 获取时间
        /// </summary>
        public string Time { get; set; }
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
