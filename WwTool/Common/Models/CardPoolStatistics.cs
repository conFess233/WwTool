using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
using WwTool.Common.Enums;

namespace WwTool.Common.Models
{
    public class CardPoolStatistics
    {
        public CardPoolType PoolType { get; set; }

        public ObservableCollection<HitGoldData> HitGoldDatas { get; set; } = new();

        public CalculateData Calculate { get; set; } = new();
    }
}
