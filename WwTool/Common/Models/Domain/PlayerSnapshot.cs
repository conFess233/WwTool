namespace WwTool.Common.Models.Domain;

public sealed class PlayerSnapshot
{
    /// <summary>
    /// 基础角色信息
    /// </summary>
    public PlayerBaseSnapshot? Base { get; set; }

    /// <summary>
    /// 摩托数据
    /// </summary>
    public MotorSnapshot? MotorData { get; set; }

    /// <summary>
    /// 车载音乐数据
    /// </summary>
    public List<MusicSnapshot>? MusicData { get; set; }

    /// <summary>
    /// 先约电台数据
    /// </summary>
    public BattlePassSnapshot? BattlePass { get; set; }
}

public sealed class PlayerBaseSnapshot
{
    /// <summary>
    /// 角色名称
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 角色 ID
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// 创建时间
    /// </summary>
    public long CreatTime { get; set; }

    /// <summary>
    /// 活跃天数
    /// </summary>
    public int ActiveDays { get; set; }

    /// <summary>
    /// 等级
    /// </summary>
    public int Level { get; set; }

    /// <summary>
    /// 索拉等级
    /// </summary>
    public int WorldLevel { get; set; }

    /// <summary>
    /// 角色数量
    /// </summary>
    public int RoleNum { get; set; }

    /// <summary>
    /// 声匣数量
    /// </summary>
    public int SoundBox { get; set; }

    /// <summary>
    /// 当前体力
    /// </summary>
    public int Energy { get; set; }

    /// <summary>
    /// 体力上限
    /// </summary>
    public int MaxEnergy { get; set; }

    /// <summary>
    /// 储备体力
    /// </summary>
    public int StoreEnergy { get; set; }

    /// <summary>
    /// 储备体力恢复时间
    /// </summary>
    public long StoreEnergyRecoverTime { get; set; }

    /// <summary>
    /// 储备体力上限
    /// </summary>
    public int MaxStoreEnergy { get; set; }

    /// <summary>
    /// 体力恢复时间
    /// </summary>
    public long EnergyRecoverTime { get; set; }

    /// <summary>
    /// 活跃度
    /// </summary>
    public int Liveness { get; set; }

    /// <summary>
    /// 活跃度上限
    /// </summary>
    public int LivenessMaxCount { get; set; }

    /// <summary>
    /// 活跃度功能是否解锁
    /// </summary>
    public bool LivenessUnlock { get; set; }

    /// <summary>
    /// 章节 ID
    /// </summary>
    public int ChapterId { get; set; }

    /// <summary>
    /// 周本次数
    /// </summary>
    public int WeeklyInstCount { get; set; }

    /// <summary>
    /// 各类箱子数量统计
    /// </summary>
    public Dictionary<string, int>? Boxes { get; set; }

    /// <summary>
    /// 箱子统计
    /// </summary>
    public Dictionary<string, int>? BasicBoxes { get; set; }

    /// <summary>
    /// 潮汐之遗
    /// </summary>
    public Dictionary<string, int>? PhantomBoxes { get; set; }

    /// <summary>
    /// 生日月份
    /// </summary>
    public int BirthMon { get; set; }

    /// <summary>
    /// 生日日期
    /// </summary>
    public int BirthDay { get; set; }

}

public sealed class MotorSnapshot
{
    /// <summary>
    /// 摩托等级
    /// </summary>
    public int Level { get; set; }

    /// <summary>
    /// 当前经验
    /// </summary>
    public int Exp { get; set; }

    /// <summary>
    /// 下一级经验
    /// </summary>
    public int NextExp { get; set; }

    /// <summary>
    /// 皮肤列表
    /// </summary>
    public List<MotorSkinSnapshot>? Skins { get; set; }

    /// <summary>
    /// 贴纸列表
    /// </summary>
    public List<MotorStickerSnapshot>? Stickers { get; set; }

    /// <summary>
    /// 装饰列表
    /// </summary>
    public List<MotorDecorationSnapshot>? Decorations { get; set; }

    /// <summary>
    /// 车架列表
    /// </summary>
    public List<MotorFrameSnapshot>? Frames { get; set; }

    /// <summary>
    /// 当前装备皮肤
    /// </summary>
    public MotorSkinSnapshot? EquipSkin { get; set; }
}

public sealed class MusicSnapshot
{
    /// <summary>
    /// 专辑编号
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// 已收集数量
    /// </summary>
    public int Count { get; set; }

    /// <summary>
    /// 总数量
    /// </summary>
    public int TotalCount { get; set; }

    public string IconPath => $"Local/Icons/{Id}.png";
}

public sealed class BattlePassSnapshot
{
    /// <summary>
    /// 电台等级
    /// </summary>
    public int Level { get; set; }

    /// <summary>
    /// 本周经验
    /// </summary>
    public int WeekExp { get; set; }

    /// <summary>
    /// 本周经验上限
    /// </summary>
    public int WeekMaxExp { get; set; }

    /// <summary>
    /// 是否解锁
    /// </summary>
    public bool IsUnlock { get; set; }

    /// <summary>
    /// 是否开启
    /// </summary>
    public bool IsOpen { get; set; }

    /// <summary>
    /// 当前经验
    /// </summary>
    public int Exp { get; set; }

    /// <summary>
    /// 升级所需经验上限
    /// </summary>
    public int ExpLimit { get; set; }
}

public sealed class MotorSkinSnapshot
{
    /// <summary>
    /// 皮肤 ID
    /// </summary>
    public int SkinId { get; set; }

    /// <summary>
    /// 品质
    /// </summary>
    public int Quality { get; set; }

    public string IconPath => $"Local/Icons/{SkinId}.png";
}

public sealed class MotorStickerSnapshot
{
    /// <summary>
    /// 贴纸 ID
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// 贴纸品质
    /// </summary>
    public int Quality { get; set; }

    /// <summary>
    /// 部位编号
    /// </summary>
    public int PartId { get; set; }

    public string IconPath => $"Local/Icons/{Id}.png";
    public string PartIconPath => $"StickerPart{PartId}Image";
}

public sealed class MotorDecorationSnapshot
{
    /// <summary>
    /// 装饰 ID
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// 装饰品质
    /// </summary>
    public int Quality { get; set; }

    /// <summary>
    /// 部位编号
    /// </summary>
    public int PartId { get; set; }

    public string IconPath => $"Local/Icons/{Id}.png";

    public string PartIconPath => $"DecPart{PartId}Image";
}

public sealed class MotorFrameSnapshot
{
    /// <summary>
    /// 车架 ID
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// 车架品质
    /// </summary>
    public int Quality { get; set; }
    public string IconPath => $"Local/Icons/{Id}.png";
}
