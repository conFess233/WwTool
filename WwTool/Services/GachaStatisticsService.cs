using System;
using System.Collections.Generic;
using System.Linq;
using WwTool.Common;
using WwTool.Common.Enums;
using WwTool.Common.Models;
using WwTool.Common.Models.Entities;
using WwTool.Common.Models.ApiResponse;
using WwTool.Common.Models.Domain;
using WwTool.Common.Utils;
using WwTool.Extensions;
using WwTool.Services.Interfaces;

namespace WwTool.Services
{
    public class GachaStatisticsService : IGachaStatisticsService
    {
        private readonly GameDataService _gameData;

        public GachaStatisticsService(GameDataService gameData)
        {
            _gameData = gameData;
        }

        public GachaStatisticsResult OrganizeData(IEnumerable<GachaPull> data, CardPoolType poolType, string languageCode)
        {
            var result = new GachaStatisticsResult();
            result.PoolStatistics.PoolType = poolType;
            bool isCharacterEventPool = poolType == CardPoolType.CharacterEvent;
            List<GachaPull> ordered = OrderChronologically(data);

            int pity = 0;
            int successCount = 0;
            int missCount = 0;
            int featuredCount = 0;
            int hitGoldCount = 0;
            bool isGuaranteedFeatured = false;

            var tempDatas = new List<HitGoldData>();
            var tempFourStars = new Dictionary<int, FourStarHistoryItem>();

            foreach (var item in ordered)
            {
                pity++;
                var itemInfo = _gameData.GetItemById(item.ResourceId);

                if (item.QualityLevel == 4)
                {
                    if (tempFourStars.TryGetValue(item.ResourceId, out var existing))
                    {
                        existing.Count++;
                    }
                    else
                    {
                        tempFourStars[item.ResourceId] = new FourStarHistoryItem
                        {
                            ResourceId = item.ResourceId,
                            IconPath = item.IconPath,
                            Name = item.Name,
                            Count = 1
                        };
                    }
                }

                if (item.QualityLevel == 5)
                {
                    bool? isMiss = null;
                    bool? isLimited = _gameData.GetLimitedStatus(item.ResourceId, item.QualityLevel,
                        isCharacterEventPool || itemInfo?.Type == "Character");
                    if (isLimited is not null)
                    {
                        isMiss = !isLimited.Value;
                        if (isCharacterEventPool)
                        {
                            if (isLimited.Value)
                            {
                                featuredCount++;
                                if (!isGuaranteedFeatured)
                                {
                                    successCount++;
                                }

                                isGuaranteedFeatured = false;
                            }
                            else
                            {
                                missCount++;
                                isGuaranteedFeatured = true;
                            }
                        }
                    }

                    tempDatas.Add(new HitGoldData
                    {
                        GachaData = item,
                        Pity = pity,
                        FourStarHistories = new System.Collections.ObjectModel.ObservableCollection<FourStarHistoryItem>(tempFourStars.Values),
                        IsMiss = isMiss
                    });

                    result.GoldValues.Add(pity);
                    string name = itemInfo?.GetName(languageCode) ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(name)) name = item.Name;
                    result.GoldLabels.Add(name);

                    hitGoldCount++;
                    pity = 0;
                    tempFourStars.Clear();
                }
            }

            if (pity > 0 && ordered.Count > 0)
            {
                tempDatas.Add(new HitGoldData
                {
                    GachaData = new GachaPull
                    {
                        CardPoolType = ordered[^1].CardPoolType,
                        ResourceId = 0,
                        Count = 1,
                        Name = LanguageManager.Instance["Msg_Pity"] ?? "Pity",
                        QualityLevel = 1,
                        ResourceType = LanguageManager.Instance["Msg_Pity"] ?? "Pity",
                        Time = ordered[^1].Time
                    },
                    Pity = pity,
                    FourStarHistories = new System.Collections.ObjectModel.ObservableCollection<FourStarHistoryItem>(tempFourStars.Values)
                });
            }

            for (int i = tempDatas.Count - 1; i >= 0; i--)
            {
                result.PoolStatistics.HitGoldDatas.Add(tempDatas[i]);
            }

            result.PoolStatistics.Calculate.Tides = ordered.Count;
            result.PoolStatistics.Calculate.HitGoldCount = hitGoldCount;
            result.PoolStatistics.Calculate.AvgGoldTide = hitGoldCount != 0 ? (double)(ordered.Count - pity) / hitGoldCount : null;

            result.SuccessCount = successCount;
            result.MissCount = missCount;
            result.FeaturedCount = featuredCount;

            return result;
        }

        public GlobalStatisticsResult CalculateGlobalStatistics(IEnumerable<CardPoolStatistics> poolStatistics, int successCount, int featuredCount)
        {
            var result = new GlobalStatisticsResult();

            foreach (var pool in poolStatistics)
            {
                result.TotalTides += pool.Calculate.Tides;
                result.TotalAstrites += pool.Calculate.Astrites;
                result.TotalHitGold += pool.Calculate.HitGoldCount;

                if (pool.PoolType == CardPoolType.CharacterEvent)
                {
                    if (pool.Calculate.HitGoldCount > 0)
                    {
                        result.SuccessRate = (double)successCount / pool.Calculate.HitGoldCount * 100;
                        result.AvgCharaTide = pool.Calculate.AvgGoldTide;
                    }

                    if (featuredCount > 0)
                    {
                        // 列表为新到旧：忽略最近一次 UP 之后尚未完成的区间。
                        int completedPulls = pool.HitGoldDatas
                            .Where(x => x.GachaData.QualityLevel == 5)
                            .SkipWhile(x => x.IsMiss != false)
                            .Sum(x => x.Pity);
                        result.AvgLimitCharaTide = (double)completedPulls / featuredCount;
                    }

                    result.LimitedGoldCount = pool.Calculate.HitGoldCount;
                }
            }

            return result;
        }

        public GachaInsights CalculateInsights(IEnumerable<GachaPull> data)
        {
            List<GachaPull> ordered = OrderChronologically(data);

            int[] pityBins = new int[8];
            var fiveStars = new List<FiveStarInsight>();
            var rarities = new Dictionary<CardPoolType, int[]>();
            var daily = new SortedDictionary<DateTime, int>();
            var cumulative = new List<CumulativePullInsight>();
            var featured = new List<FeaturedPullInsight>();
            var pityByPool = new Dictionary<CardPoolType, int>();
            int totalPulls = 0;
            int characterPulls = 0;
            int totalFiveStars = 0;
            int pullsSinceFeatured = 0;
            int validFeaturedPulls = 0;
            int validFeaturedCount = 0;

            foreach (GachaPull item in ordered)
            {
                CardPoolType? pool = ParsePoolType(item.CardPoolType);
                DateTime occurredAt = ParseTime(item.Time);
                totalPulls++;
                daily.TryGetValue(occurredAt.Date, out int dayCount);
                daily[occurredAt.Date] = dayCount + 1;

                if (pool is not null)
                {
                    if (!rarities.TryGetValue(pool.Value, out int[]? counts))
                    {
                        counts = new int[3];
                        rarities[pool.Value] = counts;
                    }
                    if (item.QualityLevel is >= 3 and <= 5) counts[item.QualityLevel - 3]++;
                    pityByPool.TryGetValue(pool.Value, out int pity);
                    pityByPool[pool.Value] = pity + 1;
                }

                if (pool == CardPoolType.CharacterEvent)
                {
                    characterPulls++;
                    pullsSinceFeatured++;
                }
                if (item.QualityLevel != 5 || pool is null) continue;

                int fiveStarPity = pityByPool[pool.Value];
                pityByPool[pool.Value] = 0;
                pityBins[Math.Clamp((fiveStarPity - 1) / 10, 0, pityBins.Length - 1)]++;
                totalFiveStars++;
                GameItemInfo? itemInfo = _gameData.GetItemById(item.ResourceId);
                bool? isFeatured = pool == CardPoolType.CharacterEvent
                    ? _gameData.GetLimitedStatus(item.ResourceId, item.QualityLevel, true)
                    : null;
                fiveStars.Add(new FiveStarInsight(occurredAt, item.Name, fiveStarPity, isFeatured));
                cumulative.Add(new CumulativePullInsight(occurredAt, totalPulls, totalFiveStars));

                if (pool == CardPoolType.CharacterEvent && isFeatured == true)
                {
                    validFeaturedPulls += pullsSinceFeatured;
                    validFeaturedCount++;
                    featured.Add(new FeaturedPullInsight(
                        featured.Count + 1,
                        item.Name,
                        characterPulls,
                        (featured.Count + 1) * GachaInsights.FeaturedExpectation,
                        (double)validFeaturedPulls / validFeaturedCount));
                    pullsSinceFeatured = 0;
                }
            }

            return new GachaInsights
            {
                PityDistribution = pityBins,
                PityLabels = ["1-10", "11-20", "21-30", "31-40", "41-50", "51-60", "61-70", "71-80"],
                FiveStars = fiveStars,
                PoolRarities = rarities.OrderBy(x => x.Key)
                    .Select(x => new PoolRarityInsight(x.Key, x.Value[0], x.Value[1], x.Value[2]))
                    .ToList(),
                DailyPulls = daily.Select(x => new DailyPullInsight(x.Key, x.Value)).ToList(),
                CumulativePulls = cumulative,
                FeaturedPulls = featured,
                CurrentCharacterPity = pityByPool.GetValueOrDefault(CardPoolType.CharacterEvent)
            };
        }

        /// <summary>
        /// 将按 SourceOrder 读取的记录转换为统计用的旧到新顺序。
        /// 新批次可能追加在旧批次之后，因此按时间归并；同时间内 API 为新到旧，
        /// 必须反转源序，不能只按时间稳定排序，否则十连中的五星位置会错位。
        /// 只排序计算副本，不修改持久化记录或同步顺序。
        /// </summary>
        private static List<GachaPull> OrderChronologically(IEnumerable<GachaPull> data)
        {
            ArgumentNullException.ThrowIfNull(data);
            return data.Select((item, index) => new { Item = item, Index = index, Time = ParseTime(item.Time) })
                .OrderBy(x => x.Time)
                .ThenByDescending(x => x.Index)
                .Select(x => x.Item)
                .ToList();
        }

        private static DateTime ParseTime(string value) =>
            DateTime.TryParse(value, out DateTime parsed) ? parsed : DateTime.MinValue;

        private static CardPoolType? ParsePoolType(string value)
        {
            if (int.TryParse(value, out int numeric) && Enum.IsDefined(typeof(CardPoolType), numeric))
                return (CardPoolType)numeric;

            foreach (CardPoolType type in Enum.GetValues<CardPoolType>())
            {
                if (type.GetDescription() == value) return type;
            }
            return null;
        }
    }
}
