using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using WwTool.Common.Context;
using WwTool.Common.Enums;
using WwTool.Common.Exceptions;
using WwTool.Common.Models;
using WwTool.Common.Models.ApiResponse;
using WwTool.Extensions;
using WwTool.Services.Interfaces;

namespace WwTool.Services.Repositories
{
    public class GachaRepository : IGachaRepository
    {
        private readonly ILoggerService _logger;
        private readonly System.Threading.SemaphoreSlim _writeLock = new System.Threading.SemaphoreSlim(1, 1);

        public GachaRepository(ILoggerService logger)
        {
            _logger = logger;
        }

        public (string? LatestTime, int Count) GetLatestRecordInfo(string uid, int poolType)
        {
            try
            {
                using var db = new AppDbContext();

                var latestTime = db.GachaRecords
                    .Where(x => x.Uid == uid && x.PoolType == poolType)
                    .OrderByDescending(x => x.Time)
                    .Select(x => x.Time)
                    .FirstOrDefault();

                if (string.IsNullOrEmpty(latestTime))
                    return (null, 0);

                int count = db.GachaRecords
                    .Count(x => x.Uid == uid && x.PoolType == poolType && x.Time == latestTime);

                return (latestTime, count);
            }
            catch (Exception ex)
            {
                throw new WwToolDatabaseException($"查询本地最新记录信息失败(Uid: {uid}, PoolType: {poolType})", ex);
            }
        }

        public void DeleteRecordsAtTime(string uid, int poolType, string time)
        {
            try
            {
                using var db = new AppDbContext();

                db.GachaRecords
                    .Where(x => x.Uid == uid && x.PoolType == poolType && x.Time == time)
                    .ExecuteDelete();
            }
            catch (Exception ex)
            {
                throw new WwToolDatabaseException($"删除本地特定时间点记录失败(Uid: {uid}, Time: {time})", ex);
            }
        }

        public void InsertRecords(IEnumerable<GachaData> records, string uid, int poolType)
        {
            if (records == null || !records.Any()) return;

            try
            {
                _logger.Debug($"正在插入 {records.Count()} 条记录 (UID: {uid}, 卡池类型: {poolType})");
                using var db = new AppDbContext();

                if (!db.UserAccounts.Any(a => a.Uid == uid))
                {
                    db.UserAccounts.Add(new UserAccount { Uid = uid });
                    db.SaveChanges();
                }

                var entities = records.Select(r => new GachaRecord
                {
                    Uid = uid,
                    PoolType = poolType,
                    ResourceId = r.ResourceId,
                    Name = r.Name,
                    ResourceType = r.ResourceType,
                    QualityLevel = r.QualityLevel,
                    Time = r.Time
                }).ToList();

                db.GachaRecords.AddRange(entities);
                db.SaveChanges();
            }
            catch (Exception ex)
            {
                throw new WwToolDatabaseException($"批量保存抽卡记录失败(Uid: {uid}, PoolType: {poolType})", ex);
            }
        }

        public async Task<List<GachaData>> GetAllRecordsByUid(string uid)
        {
            try
            {
                using var db = new AppDbContext();

                var records = await db.GachaRecords
                    .AsNoTracking()
                    .Where(x => x.Uid == uid)
                    .OrderBy(x => x.Time)
                    .ToListAsync();

                return records.Select(r => new GachaData
                {
                    ResourceId = r.ResourceId,
                    Name = r.Name != null ? string.Intern(r.Name) : "",
                    ResourceType = r.ResourceType != null ? string.Intern(r.ResourceType) : "",
                    QualityLevel = r.QualityLevel,
                    Time = r.Time
                }).ToList();
            }
            catch (Exception ex)
            {
                throw new WwToolDatabaseException($"获取账号本地所有记录失败(Uid: {uid})", ex);
            }
        }

        public async Task<List<GachaData>> GetPoolRecordsByUid(string uid, int poolType)
        {
            try
            {
                using var db = new AppDbContext();

                var records = await db.GachaRecords
                    .AsNoTracking()
                    .Where(x => x.Uid == uid && x.PoolType == poolType)
                    .ToListAsync();

                return records.Select(r => new GachaData
                {
                    ResourceId = r.ResourceId,
                    CardPoolType = string.Intern(EnumExtensions.GetDescription((CardPoolType)poolType)),
                    Name = r.Name != null ? string.Intern(r.Name) : "",
                    ResourceType = r.ResourceType != null ? string.Intern(r.ResourceType) : "",
                    QualityLevel = r.QualityLevel,
                    Time = r.Time
                }).ToList();
            }
            catch (Exception ex)
            {
                throw new WwToolDatabaseException($"获取账号指定卡池记录失败(Uid: {uid}, PoolType: {poolType})", ex);
            }
        }

        public async Task<int> SyncGachaDataAsync(string uid, int poolType, IEnumerable<GachaData>? data)
        {
            await _writeLock.WaitAsync();
            try
            {
                var (latestTimeStr, localCountAtLatestTime) = GetLatestRecordInfo(uid, poolType);

                List<GachaData> newRecordsToSave = new List<GachaData>();
                List<GachaData> sameTimeRecordsFromApi = new List<GachaData>();
                if (data == null)
                {
                    throw new WwToolDatabaseException("传入的抽卡数据为空");
                }

                _logger.Info($"正在同步 {data.Count()} 条 API 记录到本地数据库 (UID: {uid}, 卡池类型: {poolType})");

                foreach (var item in data)
                {
                    if (string.IsNullOrEmpty(latestTimeStr))
                    {
                        newRecordsToSave.Add(item);
                    }
                    else
                    {
                        int cmp = string.Compare(item.Time, latestTimeStr);

                        if (cmp > 0)
                        {
                            newRecordsToSave.Add(item);
                        }
                        else if (cmp == 0)
                        {
                            sameTimeRecordsFromApi.Add(item);
                        }
                        else
                        {
                            break;
                        }
                    }
                }

                if (sameTimeRecordsFromApi.Any())
                {
                    if (sameTimeRecordsFromApi.Count > localCountAtLatestTime)
                    {
                        DeleteRecordsAtTime(uid, poolType, latestTimeStr);
                        newRecordsToSave.AddRange(sameTimeRecordsFromApi);
                    }
                }

                if (newRecordsToSave.Any())
                {
                    InsertRecords(newRecordsToSave, uid, poolType);
                }

                return newRecordsToSave.Count();
            }
            finally
            {
                _writeLock.Release();
            }
        }
    }
}
