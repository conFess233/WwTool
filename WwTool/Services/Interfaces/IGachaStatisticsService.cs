using System.Collections.Generic;
using WwTool.Common.Enums;
using WwTool.Common.Models;
using WwTool.Common.Models.ApiResponse;

namespace WwTool.Services.Interfaces
{
    public class GachaStatisticsResult
    {
        public CardPoolStatistics PoolStatistics { get; set; } = new();
        public int SuccessCount { get; set; }
        public int MissCount { get; set; }
        public List<int> GoldValues { get; set; } = new();
        public List<string> GoldLabels { get; set; } = new();
    }

    public interface IGachaStatisticsService
    {
        GachaStatisticsResult OrganizeData(IEnumerable<GachaData> data, CardPoolType poolType, string languageCode);
        GlobalStatisticsResult CalculateGlobalStatistics(IEnumerable<CardPoolStatistics> poolStatistics, int successCount);
    }

    public class GlobalStatisticsResult
    {
        public int TotalTides { get; set; }
        public int TotalAstrites { get; set; }
        public int TotalHitGold { get; set; }
        public double SuccessRate { get; set; }
        public int LimitedGoldCount { get; set; }
        public double AvgCharaTide { get; set; }
        public double AvgLimitCharaTide { get; set; }
    }
}
