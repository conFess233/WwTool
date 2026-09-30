using Api = WwTool.Common.Models.ApiResponse;
using WwTool.Common.Models.Domain;

namespace WwTool.Services;

internal static class PlayerSnapshotMapper
{
    internal static PlayerRegionSummary? Map(Api.PlayerRegionInfo? value) => value is null ? null : new()
    {
        RoleId = value.RoleId,
        RoleName = value.RoleName,
        Level = value.Level,
        Sex = value.Sex,
        HeadPhoto = value.HeadPhoto,
    };
    internal static PlayerSnapshot? Map(Api.RoleDetailInfo? value) => value is null ? null : new()
    {
        Base = Map(value.Base),
        MotorData = Map(value.MotorData),
        MusicData = value.MusicData?.Select(x => Map(x)!).ToList(),
        BattlePass = Map(value.BattlePass),
    };

    internal static PlayerBaseSnapshot? Map(Api.RoleBaseInfo? value) => value is null ? null : new()
    {
        Name = value.Name,
        Id = value.Id,
        CreatTime = value.CreatTime,
        ActiveDays = value.ActiveDays,
        Level = value.Level,
        WorldLevel = value.WorldLevel,
        RoleNum = value.RoleNum,
        SoundBox = value.SoundBox,
        Energy = value.Energy,
        MaxEnergy = value.MaxEnergy,
        StoreEnergy = value.StoreEnergy,
        StoreEnergyRecoverTime = value.StoreEnergyRecoverTime,
        MaxStoreEnergy = value.MaxStoreEnergy,
        EnergyRecoverTime = value.EnergyRecoverTime,
        Liveness = value.Liveness,
        LivenessMaxCount = value.LivenessMaxCount,
        LivenessUnlock = value.LivenessUnlock,
        ChapterId = value.ChapterId,
        WeeklyInstCount = value.WeeklyInstCount,
        Boxes = value.Boxes is null ? null : new(value.Boxes),
        BasicBoxes = value.BasicBoxes is null ? null : new(value.BasicBoxes),
        PhantomBoxes = value.PhantomBoxes is null ? null : new(value.PhantomBoxes),
        BirthMon = value.BirthMon,
        BirthDay = value.BirthDay,
    };

    internal static MotorSnapshot? Map(Api.RoleMotorData? value) => value is null ? null : new()
    {
        Level = value.Level,
        Exp = value.Exp,
        NextExp = value.NextExp,
        Skins = value.Skins?.Select(x => Map(x)!).ToList(),
        Stickers = value.Stickers?.Select(x => Map(x)!).ToList(),
        Decorations = value.Decorations?.Select(x => Map(x)!).ToList(),
        Frames = value.Frames?.Select(x => Map(x)!).ToList(),
        EquipSkin = Map(value.EquipSkin),
    };

    internal static MusicSnapshot? Map(Api.RoleMusicData? value) => value is null ? null : new()
    {
        Id = value.Id,
        Count = value.Count,
        TotalCount = value.TotalCount,
    };

    internal static BattlePassSnapshot? Map(Api.RoleBattlePass? value) => value is null ? null : new()
    {
        Level = value.Level,
        WeekExp = value.WeekExp,
        WeekMaxExp = value.WeekMaxExp,
        IsUnlock = value.IsUnlock,
        IsOpen = value.IsOpen,
        Exp = value.Exp,
        ExpLimit = value.ExpLimit,
    };

    internal static MotorSkinSnapshot? Map(Api.MotorSkin? value) => value is null ? null : new()
    {
        SkinId = value.SkinId,
        Quality = value.Quality,
    };

    internal static MotorStickerSnapshot? Map(Api.MotorSticker? value) => value is null ? null : new()
    {
        Id = value.Id,
        Quality = value.Quality,
        PartId = value.PartId,
    };

    internal static MotorDecorationSnapshot? Map(Api.MotorDecoration? value) => value is null ? null : new()
    {
        Id = value.Id,
        Quality = value.Quality,
        PartId = value.PartId,
    };

    internal static MotorFrameSnapshot? Map(Api.MotorFrame? value) => value is null ? null : new()
    {
        Id = value.Id,
        Quality = value.Quality,
    };

}
