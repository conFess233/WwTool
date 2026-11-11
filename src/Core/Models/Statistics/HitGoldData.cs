using WwTool.Core.Models.Domain;
using System;
using System.Collections.Generic;
using System.Text;
using WwTool.Core.Models.Api.Responses;

namespace WwTool.Core.Models.Statistics
{
    public class HitGoldData
    {
        public GachaPull GachaData { get; set; } = new();
        /// <summary>按界面语言解析的名称（资料表优先，缺失时为原名）；保底占位行为本地化的“保底”。</summary>
        public string DisplayName { get; set; } = string.Empty;
        public int Pity { get; set; }
        public System.Collections.ObjectModel.ObservableCollection<FourStarHistoryItem> FourStarHistories { get; set; } = new();
        public bool? IsMiss { get; set; }
    }
}
