using System;
using System.Collections.Generic;
using System.Linq;
using WwTool.Common;
using WwTool.Common.Enums;
using WwTool.Common.Models;
using WwTool.Common.Models.ApiResponse;
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

        public GachaStatisticsResult OrganizeData(IEnumerable<GachaData> data, CardPoolType poolType, string languageCode)
        {
            var result = new GachaStatisticsResult();
            result.PoolStatistics.PoolType = poolType;
            bool isCharacterEventPool = poolType == CardPoolType.CharacterEvent;

            int pity = 0;
            int successCount = 0;
            int missCount = 0;
            int hitGoldCount = 0;

            var tempDatas = new List<HitGoldData>();
            var tempFourStars = new Dictionary<int, FourStarHistoryItem>();

            foreach (var item in data.Reverse())
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
                    if (itemInfo != null)
                    {
                        isMiss = !itemInfo.IsUp;
                        if (isCharacterEventPool)
                        {
                            if (itemInfo.IsUp) successCount++;
                            else missCount++;
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
                    string name = itemInfo != null ? itemInfo.GetName(languageCode) : item.Name;
                    result.GoldLabels.Add(name);

                    hitGoldCount++;
                    pity = 0;
                    tempFourStars.Clear();
                }
            }

            if (pity > 0 && data.Any())
            {
                tempDatas.Add(new HitGoldData
                {
                    GachaData = new GachaData
                    {
                        CardPoolType = data.First().CardPoolType,
                        ResourceId = 0,
                        Count = 1,
                        Name = LanguageManager.Instance["Msg_Pity"] ?? "Pity",
                        QualityLevel = 1,
                        ResourceType = LanguageManager.Instance["Msg_Pity"] ?? "Pity",
                        Time = data.First().Time
                    },
                    Pity = pity,
                    FourStarHistories = new System.Collections.ObjectModel.ObservableCollection<FourStarHistoryItem>(tempFourStars.Values)
                });
            }

            for (int i = tempDatas.Count - 1; i >= 0; i--)
            {
                result.PoolStatistics.HitGoldDatas.Add(tempDatas[i]);
            }

            result.PoolStatistics.Calculate.Tides = data.Count();
            result.PoolStatistics.Calculate.HitGoldCount = hitGoldCount;
            result.PoolStatistics.Calculate.AvgGoldTide = hitGoldCount != 0 ? (double)result.PoolStatistics.Calculate.Tides / hitGoldCount : 0;

            result.SuccessCount = successCount;
            result.MissCount = missCount;

            return result;
        }

        public GlobalStatisticsResult CalculateGlobalStatistics(IEnumerable<CardPoolStatistics> poolStatistics, int successCount)
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
                        result.SuccessRate = (double)successCount / pool.Calculate.HitGoldCount;
                        result.AvgCharaTide = (double)pool.Calculate.Tides / pool.Calculate.HitGoldCount;
                    }

                    if (successCount > 0)
                    {
                        result.AvgLimitCharaTide = (double)pool.Calculate.Tides / successCount;
                    }

                    result.LimitedGoldCount = pool.Calculate.HitGoldCount;
                }
            }

            return result;
        }
    }
}
