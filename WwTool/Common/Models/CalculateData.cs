using System;
using System.Collections.Generic;
using System.Text;

namespace WwTool.Common.Models
{
    public class CalculateData : BaseModel
    {
        // 总抽数
        private int _Tides;
        // 总星声花费
        private int _Astrites;
        // 总出金数
        private int _hitGoldCount;
        // 平均每金
        private double _avgGoldTide;

        public int Tides
        {
            get => _Tides;
            set
            {
                _Tides = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(Astrites));
            }
        }
        public int Astrites
        {
            get => Tides * 160;
        }

        public int HitGoldCount
        {
            get => _hitGoldCount;
            set
            {
                _hitGoldCount = value;
                OnPropertyChanged();
            }
        }

        public double AvgGoldTide
        {
            get
            {
                return _avgGoldTide;
            }
            set
            {
                _avgGoldTide = value;
                OnPropertyChanged();
            }
        }


        public void Clear()
        {
            Tides = 0;
            HitGoldCount = 0;
            AvgGoldTide = 0;
        }
    }
}