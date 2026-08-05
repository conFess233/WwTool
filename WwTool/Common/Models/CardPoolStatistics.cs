using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
using WwTool.Common.Enums;

namespace WwTool.Common.Models
{
    /// <summary>
    /// 单个卡池的统计结果，替换汇总对象时通知当前可见的 Tab 刷新绑定。
    /// </summary>
    public class CardPoolStatistics : BaseModel
    {
        private CalculateData _calculate = new();

        public CardPoolType PoolType { get; set; }

        public ObservableCollection<HitGoldData> HitGoldDatas { get; set; } = new();

        public CalculateData Calculate
        {
            get => _calculate;
            set
            {
                if (ReferenceEquals(_calculate, value)) return;
                _calculate = value;
                OnPropertyChanged();
            }
        }
    }
}
