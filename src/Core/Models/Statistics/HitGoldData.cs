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
        public int Pity { get; set; }
        public System.Collections.ObjectModel.ObservableCollection<FourStarHistoryItem> FourStarHistories { get; set; } = new();
        public bool? IsMiss { get; set; }
    }
}
