using WwTool.Common.Enums;
using WwTool.Common.Models.ApiResponse;
using WwTool.Common.Models.Domain;
using WwTool.Extensions;
using WwTool.Services.Interfaces;

namespace WwTool.Services;

/// <summary>基于页面快照准备图表数据；不访问 Dispatcher 或绑定集合。</summary>
public static class StatisticsChartDataService
{
    public sealed record Snapshot(List<GachaPull> Records, GachaInsights Insights,
        SortedDictionary<DateTime, (int Pulls, int Golds)> Days, List<string> Labels,
        List<int> Pulls, List<int> Golds, KeyValuePair<DateTime, (int Pulls, int Golds)> Peak,
        int FourCharacters, int FourWeapons, int Success, int OtherGolds,
        List<string> PoolLabels, List<int> Tides, List<double?> Averages);

    public static Snapshot Build(List<GachaPull> records, HashSet<CardPoolType> pools, int range,
        string language, IGachaStatisticsService statistics, GameDataService catalog, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var filteredDatas = records.Where(x =>
        {
            if (range == 1 && DateTime.TryParse(x.Time, out var dt1) && dt1 < DateTime.Now.AddMonths(-1)) return false;
            if (range == 2 && DateTime.TryParse(x.Time, out var dt2) && dt2 < DateTime.Now.AddMonths(-3)) return false;

            if (!pools.Contains((CardPoolType)ParsePoolType(x.CardPoolType))) return false;
            return true;
        }).ToList();

        GachaInsights insights = statistics.CalculateInsights(
            filteredDatas);

        var dailyBuckets = new SortedDictionary<DateTime, (int Pulls, int Golds)>();
        foreach (var item in filteredDatas)
        {
            token.ThrowIfCancellationRequested();
            if (!DateTime.TryParse(item.Time, out var pullTime)) continue;

            var day = pullTime.Date;
            dailyBuckets.TryGetValue(day, out var bucket);
            dailyBuckets[day] = (
                bucket.Pulls + 1,
                bucket.Golds + (item.QualityLevel == 5 ? 1 : 0));
        }

        int labelStep = Math.Max(1, (int)Math.Ceiling(dailyBuckets.Count / 12d));
        var dailyLabels = dailyBuckets.Keys
            .Select((date, index) => index % labelStep == 0 || index == dailyBuckets.Count - 1
                ? date.ToString("MM-dd")
                : string.Empty)
            .ToList();
        var dailyPulls = dailyBuckets.Values.Select(x => x.Pulls).ToList();
        var dailyGolds = dailyBuckets.Values.Select(x => x.Golds).ToList();
        var peakDay = dailyBuckets.OrderByDescending(x => x.Value.Pulls).FirstOrDefault();

        // 四星及歪率
        int fourStarCharacterCount = 0;
        int fourStarWeaponCount = 0;
        int success = 0;

        foreach (var item in filteredDatas)
        {
            token.ThrowIfCancellationRequested();
            if (item.QualityLevel == 4)
            {
                var itemInfo = catalog.GetItemById(item.ResourceId);
                string typeStr = itemInfo?.Type ?? item.ResourceType;
                if (typeStr.Contains("角色") || typeStr.Contains("Role") || typeStr.Contains("Character")) fourStarCharacterCount++;
                else fourStarWeaponCount++;
            }

        }

        var filteredCharacterEventStats = statistics.OrganizeData(
            filteredDatas.Where(x => ParsePoolType(x.CardPoolType) == (int)CardPoolType.CharacterEvent),
            CardPoolType.CharacterEvent,
            language);
        success = filteredCharacterEventStats.SuccessCount;
        int otherFiveStars = Math.Max(
            0,
            filteredCharacterEventStats.PoolStatistics.Calculate.HitGoldCount - success);

        // 比较图表
        var compareXLabels = new List<string>();
        var tidesData = new List<int>();

        var avgTideData = new List<double?>();

        foreach (var type in Enum.GetValues<CardPoolType>())
        {
            if (!pools.Contains(type)) continue;

            var pData = filteredDatas.Where(x => ParsePoolType(x.CardPoolType) == (int)type).ToList();
            if (!pData.Any()) continue;

            int tides = pData.Count;
            var poolResult = statistics.OrganizeData(pData, type, language);

            compareXLabels.Add(type.ToString());
            tidesData.Add(tides);

            avgTideData.Add(poolResult.PoolStatistics.Calculate.AvgGoldTide);
        }

        token.ThrowIfCancellationRequested();
        return new(filteredDatas, insights, dailyBuckets, dailyLabels, dailyPulls, dailyGolds, peakDay,
            fourStarCharacterCount, fourStarWeaponCount, success, otherFiveStars, compareXLabels, tidesData, avgTideData);
    }

    private static int ParsePoolType(string value)
    {
        if (int.TryParse(value, out int parsed)) return parsed;
        foreach (var type in Enum.GetValues<CardPoolType>())
            if (type.GetDescription() == value) return (int)type;
        return 0;
    }
}
