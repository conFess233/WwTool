using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using WwTool.Core.Enums;

namespace WwTool.UI.Models
{
    public class CardPoolChartData
    {
        public CardPoolType PoolType { get; set; }
        public ISeries[] GoldHistorySeries { get; set; } = [];
        public Axis[] XAxes { get; set; } = [];
    }
}
