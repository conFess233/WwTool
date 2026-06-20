using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using WwTool.Common.Enums;

namespace WwTool.Common.Models
{
    public class CardPoolChartData
    {
        public CardPoolType PoolType { get; set; }
        public ISeries[] GoldHistorySeries { get; set; }
        public Axis[] XAxes { get; set; }
    }
}
