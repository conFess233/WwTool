using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using WwTool.Common.Context;
using WwTool.Common.Exceptions;
using WwTool.Common.Models;
using WwTool.Common.Models.Entities;
using WwTool.Common.Models.ApiResponse;
using WwTool.Common.Utils;
using WwTool.Services.Interfaces;

namespace WwTool.Services.Repositories;

public sealed class PlayerInfoRepository(
    IDbContextFactory<AppDbContext> contextFactory,
    IDatabaseWriteCoordinator writeCoordinator,
    ILoggerService logger) : IPlayerInfoRepository
{
    public async Task SavePlayerRegionInfoAsync(
        PlayerRegionInfo playerRegionInfo,
        string region,
        string oauthCode,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(playerRegionInfo);
        try
        {
            await writeCoordinator.ExecuteAsync(async (db, token) =>
            {
                string uid = playerRegionInfo.RoleId;
                UserAccount? account = await db.UserAccounts.FirstOrDefaultAsync(x => x.Uid == uid, token);
                account ??= AddAccount(db, uid);
                ApplyAccount(account, playerRegionInfo, region);
                account.EncryptedOauthCode = Crypto.Encrypt(oauthCode);

                PlayerBaseInfo? baseInfo = await db.PlayerBaseInfos.FirstOrDefaultAsync(x => x.Uid == uid, token);
                if (baseInfo is null)
                {
                    baseInfo = new PlayerBaseInfo { Uid = uid };
                    db.PlayerBaseInfos.Add(baseInfo);
                }

                baseInfo.RoleName = playerRegionInfo.RoleName;
                baseInfo.Level = playerRegionInfo.Level;
                baseInfo.LastSyncedAtUtc = DateTimeOffset.UtcNow;
                await UpsertSyncStateAsync(db, uid, "Account", string.Empty, token);
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            throw new WwToolDatabaseException("本地保存玩家大区数据失败。", ex);
        }
    }

    public async Task SavePlayerRoleDataAsync(
        string uid,
        RoleDetailInfo roleDetail,
        string playerRegion,
        PlayerRegionInfo playerRegionInfo,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(roleDetail);
        try
        {
            await writeCoordinator.ExecuteAsync(async (db, token) =>
            {
                DateTimeOffset syncedAtUtc = DateTimeOffset.UtcNow;
                UserAccount? account = await db.UserAccounts.FirstOrDefaultAsync(x => x.Uid == uid, token);
                account ??= AddAccount(db, uid);
                ApplyAccount(account, playerRegionInfo, playerRegion);
                account.LastSyncedAtUtc = syncedAtUtc;

                PlayerBaseInfo? baseInfo = await db.PlayerBaseInfos.FirstOrDefaultAsync(x => x.Uid == uid, token);
                if (baseInfo is null)
                {
                    baseInfo = new PlayerBaseInfo { Uid = uid };
                    db.PlayerBaseInfos.Add(baseInfo);
                }
                MapBaseInfo(baseInfo, roleDetail, playerRegionInfo, syncedAtUtc);
                baseInfo.HasBattlePassData = roleDetail.BattlePass is not null;
                baseInfo.HasMusicData = roleDetail.MusicData is not null;

                PlayerMotorData? motor = await db.PlayerMotorData.FirstOrDefaultAsync(x => x.Uid == uid, token);
                if (motor is null)
                {
                    motor = new PlayerMotorData { Uid = uid };
                    db.PlayerMotorData.Add(motor);
                }
                MapMotor(motor, roleDetail, syncedAtUtc);

                PlayerBattlePass? battlePass = await db.PlayerBattlePasses.FirstOrDefaultAsync(x => x.Uid == uid, token);
                if (battlePass is null)
                {
                    battlePass = new PlayerBattlePass { Uid = uid };
                    db.PlayerBattlePasses.Add(battlePass);
                }
                MapBattlePass(battlePass, roleDetail, syncedAtUtc);

                List<PlayerMusicData> oldMusic = await db.PlayerMusicData.Where(x => x.Uid == uid).ToListAsync(token);
                db.PlayerMusicData.RemoveRange(oldMusic);
                foreach (RoleMusicData music in roleDetail.MusicData ?? [])
                {
                    db.PlayerMusicData.Add(new PlayerMusicData
                    {
                        Uid = uid,
                        AlbumId = music.Id,
                        Count = music.Count,
                        TotalCount = music.TotalCount,
                        LastSyncedAtUtc = syncedAtUtc
                    });
                }
                await UpsertSyncStateAsync(db, uid, "Role", string.Empty, token);
            }, cancellationToken);
            logger.Info("玩家角色快照已完整提交。");
        }
        catch (Exception ex)
        {
            throw new WwToolDatabaseException("玩家角色快照未能完整提交，已保留原数据。", ex);
        }
    }

    public async Task<RoleDetailInfo?> LoadPlayerRoleDataAsync(string uid, CancellationToken cancellationToken = default)
    {
        try
        {
            await using AppDbContext db = await contextFactory.CreateDbContextAsync(cancellationToken);
            PlayerBaseInfo? baseInfo = await db.PlayerBaseInfos.AsNoTracking().FirstOrDefaultAsync(x => x.Uid == uid, cancellationToken);
            if (baseInfo is null) return null;
            PlayerMotorData? motor = await db.PlayerMotorData.AsNoTracking().FirstOrDefaultAsync(x => x.Uid == uid, cancellationToken);
            PlayerBattlePass? battlePass = await db.PlayerBattlePasses.AsNoTracking().FirstOrDefaultAsync(x => x.Uid == uid, cancellationToken);
            List<PlayerMusicData> music = await db.PlayerMusicData.AsNoTracking().Where(x => x.Uid == uid).OrderBy(x => x.AlbumId).ToListAsync(cancellationToken);
            return MapRoleDetail(baseInfo, motor, battlePass, music);
        }
        catch (Exception ex)
        {
            throw new WwToolDatabaseException("加载本地玩家角色快照失败。", ex);
        }
    }

    private static UserAccount AddAccount(AppDbContext db, string uid)
    {
        var account = new UserAccount { Uid = uid };
        db.UserAccounts.Add(account);
        return account;
    }

    private static void ApplyAccount(UserAccount account, PlayerRegionInfo source, string region)
    {
        account.Name = source.RoleName;
        account.Level = source.Level;
        account.Sex = source.Sex;
        account.HeadPhoto = source.HeadPhoto;
        account.Region = region;
        account.LastSyncedAtUtc = DateTimeOffset.UtcNow;
    }

    private static void MapBaseInfo(PlayerBaseInfo target, RoleDetailInfo source, PlayerRegionInfo region, DateTimeOffset syncedAtUtc)
    {
        RoleBaseInfo? value = source.Base;
        target.RoleName = region.RoleName;
        target.Level = value?.Level ?? region.Level;
        target.WorldLevel = value?.WorldLevel ?? 0;
        target.ActiveDays = value?.ActiveDays ?? 0;
        target.RoleNum = value?.RoleNum ?? 0;
        target.SoundBox = value?.SoundBox ?? 0;
        target.Energy = value?.Energy ?? 0;
        target.MaxEnergy = value?.MaxEnergy ?? 0;
        target.StoreEnergy = value?.StoreEnergy ?? 0;
        target.MaxStoreEnergy = value?.MaxStoreEnergy ?? 0;
        target.Liveness = value?.Liveness ?? 0;
        target.LivenessMaxCount = value?.LivenessMaxCount ?? 0;
        target.LivenessUnlock = value?.LivenessUnlock ?? false;
        target.ChapterId = value?.ChapterId ?? 0;
        target.WeeklyInstCount = value?.WeeklyInstCount ?? 0;
        target.CreatTime = value?.CreatTime ?? 0;
        target.BirthMon = value?.BirthMon ?? 0;
        target.BirthDay = value?.BirthDay ?? 0;
        target.EnergyRecoverTime = value?.EnergyRecoverTime ?? 0;
        target.StoreEnergyRecoverTime = value?.StoreEnergyRecoverTime ?? 0;
        target.HasBoxesData = value?.Boxes is not null;
        target.HasBasicBoxesData = value?.BasicBoxes is not null;
        target.HasPhantomBoxesData = value?.PhantomBoxes is not null;
        target.BoxesJson = JsonSerializer.Serialize(value?.Boxes ?? new Dictionary<string, int>());
        target.BasicBoxesJson = JsonSerializer.Serialize(value?.BasicBoxes ?? new Dictionary<string, int>());
        target.PhantomBoxesJson = JsonSerializer.Serialize(value?.PhantomBoxes ?? new Dictionary<string, int>());
        target.LastSyncedAtUtc = syncedAtUtc;
    }

    private static void MapMotor(PlayerMotorData target, RoleDetailInfo source, DateTimeOffset syncedAtUtc)
    {
        RoleMotorData? value = source.MotorData;
        target.Level = value?.Level ?? 0;
        target.Exp = value?.Exp ?? 0;
        target.NextExp = value?.NextExp ?? 0;
        target.SkinsJson = JsonSerializer.Serialize(value?.Skins ?? []);
        target.StickersJson = JsonSerializer.Serialize(value?.Stickers ?? []);
        target.DecorationsJson = JsonSerializer.Serialize(value?.Decorations ?? []);
        target.FramesJson = JsonSerializer.Serialize(value?.Frames ?? []);
        target.EquipSkinId = value?.EquipSkin?.SkinId ?? 0;
        target.EquipSkinQuality = value?.EquipSkin?.Quality ?? 0;
        target.LastSyncedAtUtc = syncedAtUtc;
    }

    private static void MapBattlePass(PlayerBattlePass target, RoleDetailInfo source, DateTimeOffset syncedAtUtc)
    {
        RoleBattlePass? value = source.BattlePass;
        target.Level = value?.Level ?? 0;
        target.WeekExp = value?.WeekExp ?? 0;
        target.WeekMaxExp = value?.WeekMaxExp ?? 0;
        target.IsUnlock = value?.IsUnlock ?? false;
        target.IsOpen = value?.IsOpen ?? false;
        target.Exp = value?.Exp ?? 0;
        target.ExpLimit = value?.ExpLimit ?? 0;
        target.LastSyncedAtUtc = syncedAtUtc;
    }

    private static async Task UpsertSyncStateAsync(
        AppDbContext db,
        string uid,
        string dataKind,
        string scopeKey,
        CancellationToken cancellationToken)
    {
        SyncState? state = db.SyncStates.Local.FirstOrDefault(x => x.Uid == uid && x.DataKind == dataKind && x.ScopeKey == scopeKey);
        state ??= await db.SyncStates.FirstOrDefaultAsync(
            x => x.Uid == uid && x.DataKind == dataKind && x.ScopeKey == scopeKey,
            cancellationToken);
        if (state is null)
        {
            state = new SyncState { Uid = uid, DataKind = dataKind, ScopeKey = scopeKey };
            db.SyncStates.Add(state);
        }
        state.LastSuccessfulSyncAtUtc = DateTimeOffset.UtcNow;
    }

    private static RoleDetailInfo MapRoleDetail(PlayerBaseInfo baseInfo, PlayerMotorData? motor, PlayerBattlePass? battlePass, IReadOnlyList<PlayerMusicData> music) => new()
    {
        Base = new RoleBaseInfo
        {
            Name = baseInfo.RoleName, Id = long.TryParse(baseInfo.Uid, out long id) ? id : 0,
            CreatTime = baseInfo.CreatTime, ActiveDays = baseInfo.ActiveDays, Level = baseInfo.Level,
            WorldLevel = baseInfo.WorldLevel, RoleNum = baseInfo.RoleNum, SoundBox = baseInfo.SoundBox,
            Energy = baseInfo.Energy, MaxEnergy = baseInfo.MaxEnergy, StoreEnergy = baseInfo.StoreEnergy,
            MaxStoreEnergy = baseInfo.MaxStoreEnergy, Liveness = baseInfo.Liveness,
            LivenessMaxCount = baseInfo.LivenessMaxCount, LivenessUnlock = baseInfo.LivenessUnlock,
            ChapterId = baseInfo.ChapterId, WeeklyInstCount = baseInfo.WeeklyInstCount,
            BirthMon = baseInfo.BirthMon, BirthDay = baseInfo.BirthDay,
            EnergyRecoverTime = baseInfo.EnergyRecoverTime, StoreEnergyRecoverTime = baseInfo.StoreEnergyRecoverTime,
            Boxes = baseInfo.HasBoxesData ? Deserialize<Dictionary<string, int>>(baseInfo.BoxesJson) : null,
            BasicBoxes = baseInfo.HasBasicBoxesData ? Deserialize<Dictionary<string, int>>(baseInfo.BasicBoxesJson) : null,
            PhantomBoxes = baseInfo.HasPhantomBoxesData ? Deserialize<Dictionary<string, int>>(baseInfo.PhantomBoxesJson) : null
        },
        BattlePass = !baseInfo.HasBattlePassData || battlePass is null ? null : new RoleBattlePass
        {
            Level = battlePass.Level, WeekExp = battlePass.WeekExp, WeekMaxExp = battlePass.WeekMaxExp,
            IsUnlock = battlePass.IsUnlock, IsOpen = battlePass.IsOpen, Exp = battlePass.Exp, ExpLimit = battlePass.ExpLimit
        },
        MotorData = motor is null ? null : new RoleMotorData
        {
            Level = motor.Level, Exp = motor.Exp, NextExp = motor.NextExp,
            Skins = Deserialize<List<MotorSkin>>(motor.SkinsJson), Stickers = Deserialize<List<MotorSticker>>(motor.StickersJson),
            Decorations = Deserialize<List<MotorDecoration>>(motor.DecorationsJson), Frames = Deserialize<List<MotorFrame>>(motor.FramesJson),
            EquipSkin = motor.EquipSkinId == 0 ? null : new MotorSkin { SkinId = motor.EquipSkinId, Quality = motor.EquipSkinQuality }
        },
        MusicData = baseInfo.HasMusicData
            ? music.Select(x => new RoleMusicData { Id = x.AlbumId, Count = x.Count, TotalCount = x.TotalCount }).ToList()
            : null
    };

    private static T Deserialize<T>(string json) where T : new() =>
        string.IsNullOrWhiteSpace(json) ? new T() : JsonSerializer.Deserialize<T>(json) ?? new T();
}
