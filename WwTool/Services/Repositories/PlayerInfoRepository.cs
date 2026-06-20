using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using WwTool.Common.Context;
using WwTool.Common.Exceptions;
using WwTool.Common.Models;
using WwTool.Common.Models.ApiResponse;
using WwTool.Services.Interfaces;

namespace WwTool.Services.Repositories
{
    public class PlayerInfoRepository : IPlayerInfoRepository
    {
        private readonly ILoggerService _logger;
        private readonly System.Threading.SemaphoreSlim _writeLock = new System.Threading.SemaphoreSlim(1, 1);

        public PlayerInfoRepository(ILoggerService logger)
        {
            _logger = logger;
        }

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
