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

namespace WwTool.Services
{
    /// <summary>
    /// 本地数据服务
    /// </summary>
    public class LocalDataService
    {
        private readonly ILoggerService _logger;
        private readonly System.Threading.SemaphoreSlim _writeLock = new System.Threading.SemaphoreSlim(1, 1);

        public LocalDataService(ILoggerService logger)
        {
            _logger = logger;
        }

        public async Task InitializeAsync()
        {
            await _writeLock.WaitAsync();
            try
            {
                _logger.Info("正在初始化本地数据库...");
                // 在后台线程执行数据库创建和检查
                await Task.Run(() =>
                {
                    using var db = new AppDbContext();
                    db.Database.EnsureCreated();
                });
            }
            catch (Exception ex)
            {
                throw new WwToolDatabaseException("初始化本地数据库失败", ex);
            }
            finally
            {
                _writeLock.Release();
            }
        }

        /// <summary>
        /// 获取本地最新一条记录的时间和该时间点的记录总数
        /// </summary>
        public (string? LatestTime, int Count) GetLatestRecordInfo(string uid, int poolType)
        {
            try
            {
                using var db = new AppDbContext();

                // 获取最新的时间点
                var latestTime = db.GachaRecords
                    .Where(x => x.Uid == uid && x.PoolType == poolType)
                    .OrderByDescending(x => x.Time)
                    .Select(x => x.Time)
                    .FirstOrDefault();

                if (string.IsNullOrEmpty(latestTime))
                    return (null, 0);

                // 统计该时间点共有多少条记录
                int count = db.GachaRecords
                    .Count(x => x.Uid == uid && x.PoolType == poolType && x.Time == latestTime);

                return (latestTime, count);
            }
            catch (Exception ex)
            {
                throw new WwToolDatabaseException($"查询本地最新记录信息失败(Uid: {uid}, PoolType: {poolType})", ex);
            }
        }

        /// <summary>
        /// 删除特定时间点的所有记录
        /// </summary>
        public void DeleteRecordsAtTime(string uid, int poolType, string time)
        {
            try
            {
                using var db = new AppDbContext();

                // 批量删除
                db.GachaRecords
                    .Where(x => x.Uid == uid && x.PoolType == poolType && x.Time == time)
                    .ExecuteDelete();
            }
            catch (Exception ex)
            {
                throw new WwToolDatabaseException($"删除本地特定时间点记录失败(Uid: {uid}, Time: {time})", ex);
            }
        }

        /// <summary>
        /// 批量插入新抽卡记录
        /// </summary>
        public void InsertRecords(IEnumerable<GachaData> records, string uid, int poolType)
        {
            if (records == null || !records.Any()) return;

            try
            {
                _logger.Debug($"正在插入 {records.Count()} 条记录 (UID: {uid}, 卡池类型: {poolType})");
                using var db = new AppDbContext();

                // 检查账号表中是否存在该 UID，不存在则先创建
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

        /// <summary>
        /// 获取某个账号的全量抽卡历史数据供前端图表渲染
        /// </summary>
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
                    Name = r.Name ?? "",
                    ResourceType = r.ResourceType ?? "",
                    QualityLevel = r.QualityLevel,
                    Time = r.Time
                }).ToList();
            }
            catch (Exception ex)
            {
                throw new WwToolDatabaseException($"获取账号本地所有记录失败(Uid: {uid})", ex);
            }
        }

        /// <summary>
        /// 获取某个账号的指定卡池的历史数据
        /// </summary>
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
                    CardPoolType = EnumExtensions.GetDescription((CardPoolType)poolType), // 将卡池类型转换为描述
                    Name = r.Name ?? "",
                    ResourceType = r.ResourceType ?? "",
                    QualityLevel = r.QualityLevel,
                    Time = r.Time
                }).ToList();
            }
            catch (Exception ex)
            {
                throw new WwToolDatabaseException($"获取账号指定卡池记录失败(Uid: {uid}, PoolType: {poolType})", ex);
            }
        }

        /// <summary>
        /// 增量同步云端抽卡数据到本地数据库
        /// </summary>
        /// <returns>返回本次同步的新数据</returns>
        public async Task<int> SyncGachaDataAsync(string uid, int poolType, IEnumerable<GachaData>? data)
        {
            await _writeLock.WaitAsync();
            try
            {
                // 获取本地最迟的抽卡记录
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
                        // 本地为空，首次全量同步
                        newRecordsToSave.Add(item);
                    }
                    else
                    {
                        // 基于时间的字符串字典序比对
                        int cmp = string.Compare(item.Time, latestTimeStr);

                        if (cmp > 0)
                        {
                            // API 时间 > 本地最新时间：新数据
                            newRecordsToSave.Add(item);
                        }
                        else if (cmp == 0)
                        {
                            // API 时间 == 本地最新时间：十连抽边缘冲突，暂存观察
                            sameTimeRecordsFromApi.Add(item);
                        }
                        else
                        {
                            // API 时间 < 本地最新时间：遇到老数据
                            break;
                        }
                    }
                }

                // 处理同一秒内(十连抽)的边缘数据
                if (sameTimeRecordsFromApi.Any())
                {
                    // API 返回的该秒数据量 > 本地存的该秒数据量
                    if (sameTimeRecordsFromApi.Count > localCountAtLatestTime)
                    {
                        // 回滚本地残缺数据，用 API 的覆盖
                        DeleteRecordsAtTime(uid, poolType, latestTimeStr);
                        newRecordsToSave.AddRange(sameTimeRecordsFromApi);
                    }
                }

                // 保存新数据
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

        /// <summary>
        /// 获取指定账号信息
        /// </summary>
        public async Task<UserAccount?> GetUserAccountAsync(string uid)
        {
            try
            {
                using var db = new AppDbContext();
                return await db.UserAccounts.AsNoTracking().FirstOrDefaultAsync(x => x.Uid == uid);
            }
            catch (Exception ex)
            {
                throw new WwToolDatabaseException($"获取本地账号信息失败(Uid: {uid})", ex);
            }
        }

        /// <summary>
        /// 获取所有本地账号
        /// </summary>
        public async Task<List<UserAccount>> GetAllUserAccountAsync()
        {
            try
            {
                using var db = new AppDbContext();
                return await db.UserAccounts.ToListAsync();
            }
            catch (Exception ex)
            {
                throw new WwToolDatabaseException("获取本地已存储账号列表失败", ex);
            }
        }

        /// <summary>
        /// 删除指定账号及其所有相关信息
        /// </summary>
        public async Task DeleteUserAccountAsync(string uid)
        {
            await _writeLock.WaitAsync();
            try
            {
                using var db = new AppDbContext();
                var account = await db.UserAccounts.FirstOrDefaultAsync(x => x.Uid == uid);
                if (account != null)
                {
                    db.UserAccounts.Remove(account);
                    await db.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                throw new WwToolDatabaseException($"删除本地账号及数据失败(Uid: {uid})", ex);
            }
            finally
            {
                _writeLock.Release();
            }
        }

        /// <summary>
        /// 保存获取到的OauthCode，使用AES加密
        /// </summary>
        /// <param name="uid"></param>
        /// <param name="oauthCode"></param>
        /// <returns></returns>
        /// <exception cref="WwToolDatabaseException"></exception>
        public async Task SaveOauthCodeAsync(string uid, string oauthCode)
        {
            await _writeLock.WaitAsync();
            try
            {
                using var db = new AppDbContext();
                var account = await db.UserAccounts.FirstOrDefaultAsync(x => x.Uid == uid);
                if (account == null)
                {
                    account = new UserAccount { Uid = uid };
                    db.UserAccounts.Add(account);
                }
                account.EncryptedOauthCode = WwTool.Common.Utils.Crypto.Encrypt(oauthCode);
                await db.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                throw new WwToolDatabaseException($"本地保存 OauthCode 失败(Uid: {uid})", ex);
            }
            finally
            {
                _writeLock.Release();
            }
        }

        /// <summary>
        /// 获取OauthCode
        /// </summary>
        /// <param name="uid"></param>
        /// <returns></returns>
        public async Task<string?> GetOauthCodeAsync(string uid)
        {
            try
            {
                using var db = new AppDbContext();
                var account = await db.UserAccounts.FirstOrDefaultAsync(x => x.Uid == uid);
                if (account == null || string.IsNullOrEmpty(account.EncryptedOauthCode))
                    return null;
                return WwTool.Common.Utils.Crypto.Decrypt(account.EncryptedOauthCode);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 保存大区和基础玩家信息
        /// </summary>
        public async Task SavePlayerRegionInfoAsync(PlayerRegionInfo playerRegionInfo, string region, string oauthCode)
        {
            await _writeLock.WaitAsync();
            try
            {
                using var db = new AppDbContext();

                string uid = playerRegionInfo.RoleId;
                var account = await db.UserAccounts.FirstOrDefaultAsync(x => x.Uid == uid);

                if (account == null)
                {
                    account = new UserAccount
                    {
                        Uid = uid,
                        Name = playerRegionInfo.RoleName,
                        Level = playerRegionInfo.Level,
                        Sex = playerRegionInfo.Sex,
                        HeadPhoto = playerRegionInfo.HeadPhoto,
                        Region = region,
                        EncryptedOauthCode = WwTool.Common.Utils.Crypto.Encrypt(oauthCode)
                    };
                    db.UserAccounts.Add(account);
                }
                else
                {
                    account.Name = playerRegionInfo.RoleName;
                    account.Level = playerRegionInfo.Level;
                    account.Sex = playerRegionInfo.Sex;
                    account.HeadPhoto = playerRegionInfo.HeadPhoto;
                    account.Region = region;
                    account.EncryptedOauthCode = WwTool.Common.Utils.Crypto.Encrypt(oauthCode);
                }

                var baseInfo = await db.PlayerBaseInfos.FirstOrDefaultAsync(x => x.Uid == uid);
                if (baseInfo == null)
                {
                    baseInfo = new PlayerBaseInfo
                    {
                        Uid = uid,
                        RoleName = playerRegionInfo.RoleName,
                        Level = playerRegionInfo.Level
                    };
                    db.PlayerBaseInfos.Add(baseInfo);
                }
                else
                {
                    baseInfo.RoleName = playerRegionInfo.RoleName;
                    baseInfo.Level = playerRegionInfo.Level;
                }

                await db.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                throw new WwToolDatabaseException($"本地保存玩家大区数据失败(Uid: {playerRegionInfo?.RoleId})", ex);
            }
            finally
            {
                _writeLock.Release();
            }
        }


        /// <summary>
        /// 保存账号详细数据
        /// </summary>
        /// <param name="uid"></param>
        /// <param name="roleDetail"></param>
        /// <param name="playerRegion"></param>
        /// <returns></returns>
        /// <exception cref="WwToolDatabaseException"></exception>
        public async Task SavePlayerRoleDataAsync(string uid, RoleDetailInfo roleDetail, string playerRegion, PlayerRegionInfo playerRegionInfo)
        {
            await _writeLock.WaitAsync();
            try
            {
                using var db = new AppDbContext();
                using var transaction = await db.Database.BeginTransactionAsync();
                try
                {
                    _logger.Debug($"正在保存玩家角色数据 (UID: {uid})");

                    var account = await db.UserAccounts.FirstOrDefaultAsync(x => x.Uid == uid);

                    if (account == null)
                    {
                        account = new UserAccount
                        {
                            Uid = playerRegionInfo.RoleId,
                            Name = playerRegionInfo.RoleName,
                            Level = playerRegionInfo.Level,
                            Sex = playerRegionInfo.Sex,
                            HeadPhoto = playerRegionInfo.HeadPhoto,
                            Region = playerRegion

                        };
                        db.UserAccounts.Add(account);
                    }
                    else
                    {
                        account.Uid = playerRegionInfo.RoleId;
                        account.Name = playerRegionInfo.RoleName;
                        account.Level = playerRegionInfo.Level;
                        account.Sex = playerRegionInfo.Sex;
                        account.HeadPhoto = playerRegionInfo.HeadPhoto;
                        account.Region = playerRegion;
                    }

                    // 角色基本数据
                    var baseInfo = await db.PlayerBaseInfos.FirstOrDefaultAsync(x => x.Uid == uid);
                    bool isNewBase = baseInfo == null;
                    if (isNewBase) baseInfo = new PlayerBaseInfo { Uid = uid };

                    baseInfo.RoleName = playerRegionInfo.RoleName;
                    baseInfo.Level = roleDetail.Base?.Level ?? playerRegionInfo.Level;
                    baseInfo.WorldLevel = roleDetail.Base?.WorldLevel ?? 0;
                    baseInfo.ActiveDays = roleDetail.Base?.ActiveDays ?? 0;
                    baseInfo.RoleNum = roleDetail.Base?.RoleNum ?? 0;
                    baseInfo.SoundBox = roleDetail.Base?.SoundBox ?? 0;
                    baseInfo.Energy = roleDetail.Base?.Energy ?? 0;
                    baseInfo.MaxEnergy = roleDetail.Base?.MaxEnergy ?? 0;
                    baseInfo.StoreEnergy = roleDetail.Base?.StoreEnergy ?? 0;
                    baseInfo.MaxStoreEnergy = roleDetail.Base?.MaxStoreEnergy ?? 0;
                    baseInfo.Liveness = roleDetail.Base?.Liveness ?? 0;
                    baseInfo.LivenessMaxCount = roleDetail.Base?.LivenessMaxCount ?? 0;
                    baseInfo.LivenessUnlock = roleDetail.Base?.LivenessUnlock ?? false;
                    baseInfo.WeeklyInstCount = roleDetail.Base?.WeeklyInstCount ?? 0;
                    baseInfo.CreatTime = roleDetail.Base?.CreatTime ?? 0;
                    baseInfo.BirthMon = roleDetail.Base?.BirthMon ?? 0;
                    baseInfo.BirthDay = roleDetail.Base?.BirthDay ?? 0;
                    baseInfo.EnergyRecoverTime = roleDetail.Base?.EnergyRecoverTime ?? (long)0;
                    baseInfo.StoreEnergyRecoverTime = roleDetail.Base?.StoreEnergyRecoverTime ?? (long)0;
                    baseInfo.BoxesJson = System.Text.Json.JsonSerializer.Serialize(roleDetail.Base?.Boxes ?? new Dictionary<string, int>());
                    baseInfo.BasicBoxesJson = System.Text.Json.JsonSerializer.Serialize(roleDetail.Base?.BasicBoxes ?? new Dictionary<string, int>());
                    baseInfo.PhantomBoxesJson = System.Text.Json.JsonSerializer.Serialize(roleDetail.Base?.PhantomBoxes ?? new Dictionary<string, int>());

                    if (isNewBase) db.PlayerBaseInfos.Add(baseInfo);

                    // 摩托数据
                    var motorData = await db.PlayerMotorData.FirstOrDefaultAsync(x => x.Uid == uid);
                    bool isNewMotor = motorData == null;
                    if (isNewMotor) motorData = new PlayerMotorData { Uid = uid };

                    motorData.Level = roleDetail.MotorData?.Level ?? 0;
                    motorData.Exp = roleDetail.MotorData?.Exp ?? 0;
                    motorData.NextExp = roleDetail.MotorData?.NextExp ?? 0;
                    motorData.SkinsJson = System.Text.Json.JsonSerializer.Serialize(roleDetail.MotorData?.Skins ?? new List<MotorSkin>());
                    motorData.StickersJson = System.Text.Json.JsonSerializer.Serialize(roleDetail.MotorData?.Stickers ?? new List<MotorSticker>());
                    motorData.DecorationsJson = System.Text.Json.JsonSerializer.Serialize(roleDetail.MotorData?.Decorations ?? new List<MotorDecoration>());
                    motorData.FramesJson = System.Text.Json.JsonSerializer.Serialize(roleDetail.MotorData?.Frames ?? new List<MotorFrame>());
                    motorData.EquipSkinId = roleDetail.MotorData?.EquipSkin?.SkinId ?? 0;
                    motorData.EquipSkinQuality = roleDetail.MotorData?.EquipSkin?.Quality ?? 0;

                    if (isNewMotor) db.PlayerMotorData.Add(motorData);

                    // 电台数据
                    var bpData = await db.PlayerBattlePasses.FirstOrDefaultAsync(x => x.Uid == uid);
                    bool isNewBp = bpData == null;
                    if (isNewBp) bpData = new PlayerBattlePass { Uid = uid };

                    bpData.Level = roleDetail.BattlePass?.Level ?? 0;
                    bpData.WeekExp = roleDetail.BattlePass?.WeekExp ?? 0;
                    bpData.WeekMaxExp = roleDetail.BattlePass?.WeekMaxExp ?? 0;
                    bpData.IsUnlock = roleDetail.BattlePass?.IsUnlock ?? false;
                    bpData.IsOpen = roleDetail.BattlePass?.IsOpen ?? false;
                    bpData.Exp = roleDetail.BattlePass?.Exp ?? 0;
                    bpData.ExpLimit = roleDetail.BattlePass?.ExpLimit ?? 0;

                    if (isNewBp) db.PlayerBattlePasses.Add(bpData);

                    // 音乐数据
                    var existingMusic = db.PlayerMusicData.Where(x => x.Uid == uid);
                    db.PlayerMusicData.RemoveRange(existingMusic);

                    if (roleDetail.MusicData != null)
                    {
                        foreach (var music in roleDetail.MusicData)
                        {
                            db.PlayerMusicData.Add(new PlayerMusicData
                            {
                                Uid = uid,
                                AlbumId = music.Id,
                                Count = music.Count,
                                TotalCount = music.TotalCount
                            });
                        }
                    }

                    await db.SaveChangesAsync();
                    await transaction.CommitAsync();
                }
                catch (Exception)
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            }
            catch (Exception ex)
            {
                throw new WwToolDatabaseException($"本地保存玩家角色详情数据失败(Uid: {uid})", ex);
            }
            finally
            {
                _writeLock.Release();
            }
        }

        /// <summary>
        /// 加载本地保存的角色详情
        /// </summary>
        /// <param name="uid"></param>
        /// <returns></returns>
        /// <exception cref="WwToolDatabaseException"></exception>
        public async Task<RoleDetailInfo?> LoadPlayerRoleDataAsync(string uid)
        {
            try
            {
                using var db = new AppDbContext();

                var baseInfo = await db.PlayerBaseInfos.FirstOrDefaultAsync(x => x.Uid == uid);
                if (baseInfo == null) return null;

                var motorData = await db.PlayerMotorData.FirstOrDefaultAsync(x => x.Uid == uid);
                var bpData = await db.PlayerBattlePasses.FirstOrDefaultAsync(x => x.Uid == uid);
                var musicList = await db.PlayerMusicData.Where(x => x.Uid == uid).ToListAsync();

                var roleDetail = new RoleDetailInfo
                {
                    Base = new RoleBaseInfo
                    {
                        Name = baseInfo.RoleName,
                        Id = long.TryParse(baseInfo.Uid, out long parsedUid) ? parsedUid : 0,
                        CreatTime = baseInfo.CreatTime,
                        ActiveDays = baseInfo.ActiveDays,
                        Level = baseInfo.Level,
                        WorldLevel = baseInfo.WorldLevel,
                        RoleNum = baseInfo.RoleNum,
                        SoundBox = baseInfo.SoundBox,
                        Energy = baseInfo.Energy,
                        MaxEnergy = baseInfo.MaxEnergy,
                        StoreEnergy = baseInfo.StoreEnergy,
                        MaxStoreEnergy = baseInfo.MaxStoreEnergy,
                        Liveness = baseInfo.Liveness,
                        LivenessMaxCount = baseInfo.LivenessMaxCount,
                        LivenessUnlock = baseInfo.LivenessUnlock,
                        WeeklyInstCount = baseInfo.WeeklyInstCount,
                        BirthMon = baseInfo.BirthMon,
                        BirthDay = baseInfo.BirthDay,
                        EnergyRecoverTime = baseInfo.EnergyRecoverTime,
                        StoreEnergyRecoverTime = baseInfo.StoreEnergyRecoverTime,

                        Boxes = string.IsNullOrEmpty(baseInfo.BoxesJson)
                            ? new Dictionary<string, int>()
                            : System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, int>>(baseInfo.BoxesJson) ?? new Dictionary<string, int>(),
                        BasicBoxes = string.IsNullOrEmpty(baseInfo.BasicBoxesJson)
                            ? new Dictionary<string, int>()
                            : System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, int>>(baseInfo.BasicBoxesJson) ?? new Dictionary<string, int>(),
                        PhantomBoxes = string.IsNullOrEmpty(baseInfo.PhantomBoxesJson)
                            ? new Dictionary<string, int>()
                            : System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, int>>(baseInfo.PhantomBoxesJson) ?? new Dictionary<string, int>()
                    },
                    BattlePass = bpData == null ? null : new RoleBattlePass
                    {
                        Level = bpData.Level,
                        WeekExp = bpData.WeekExp,
                        WeekMaxExp = bpData.WeekMaxExp,
                        IsUnlock = bpData.IsUnlock,
                        IsOpen = bpData.IsOpen,
                        Exp = bpData.Exp,
                        ExpLimit = bpData.ExpLimit
                    },
                    MotorData = motorData == null ? null : new RoleMotorData
                    {
                        Level = motorData.Level,
                        Exp = motorData.Exp,
                        NextExp = motorData.NextExp,
                        Skins = string.IsNullOrEmpty(motorData.SkinsJson)
                            ? new List<MotorSkin>()
                            : System.Text.Json.JsonSerializer.Deserialize<List<MotorSkin>>(motorData.SkinsJson) ?? new List<MotorSkin>(),
                        Stickers = string.IsNullOrEmpty(motorData.StickersJson)
                            ? new List<MotorSticker>()
                            : System.Text.Json.JsonSerializer.Deserialize<List<MotorSticker>>(motorData.StickersJson) ?? new List<MotorSticker>(),
                        Decorations = string.IsNullOrEmpty(motorData.DecorationsJson)
                            ? new List<MotorDecoration>()
                            : System.Text.Json.JsonSerializer.Deserialize<List<MotorDecoration>>(motorData.DecorationsJson) ?? new List<MotorDecoration>(),
                        Frames = string.IsNullOrEmpty(motorData.FramesJson)
                            ? new List<MotorFrame>()
                            : System.Text.Json.JsonSerializer.Deserialize<List<MotorFrame>>(motorData.FramesJson) ?? new List<MotorFrame>(),
                        EquipSkin = motorData.EquipSkinId == 0 ? null : new MotorSkin
                        {
                            SkinId = motorData.EquipSkinId,
                            Quality = motorData.EquipSkinQuality
                        }
                    },
                    MusicData = musicList.Select(x => new RoleMusicData
                    {
                        Id = x.AlbumId,
                        Count = x.Count,
                        TotalCount = x.TotalCount
                    }).ToList()
                };

                return roleDetail;
            }
            catch (Exception ex)
            {
                throw new WwToolDatabaseException($"加载本地玩家角色详情数据失败(Uid: {uid})", ex);
            }
        }
    }
}
