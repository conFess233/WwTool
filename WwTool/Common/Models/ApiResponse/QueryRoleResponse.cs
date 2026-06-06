using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace WwTool.Common.Models.ApiResponse
{
    /// <summary>
    /// 玩家角色详情响应体
    /// </summary>
    public class QueryRoleResponse
    {
        [JsonPropertyName("code")]
        public int Code { get; set; }

        [JsonPropertyName("message")]
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// 按大区返回的角色详情
        /// </summary>
        [JsonPropertyName("data")]
        public Dictionary<string, string>? Data { get; set; }

        [JsonPropertyName("timestamp")]
        public long Timestamp { get; set; }
    }

    /// <summary>
    /// 角色详细信息
    /// </summary>
    public class RoleDetailInfo
    {
        /// <summary>
        /// 基础角色信息
        /// </summary>
        [JsonPropertyName("Base")]
        public RoleBaseInfo? Base { get; set; }

        /// <summary>
        /// 摩托数据
        /// </summary>
        [JsonPropertyName("MotorData")]
        public RoleMotorData? MotorData { get; set; }

        /// <summary>
        /// 车载音乐数据
        /// </summary>
        [JsonPropertyName("MusicData")]
        [JsonConverter(typeof(WwTool.Common.Converters.MusicDataConverter))]
        public List<RoleMusicData>? MusicData { get; set; }

        /// <summary>
        /// 先约电台数据
        /// </summary>
        [JsonPropertyName("BattlePass")]
        public RoleBattlePass? BattlePass { get; set; }
    }

    /// <summary>
    /// 基础角色信息
    /// </summary>
    public class RoleBaseInfo
    {
        /// <summary>
        /// 角色名称
        /// </summary>
        [JsonPropertyName("Name")]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// 角色 ID
        /// </summary>
        [JsonPropertyName("Id")]
        public int Id { get; set; }

        /// <summary>
        /// 创建时间
        /// </summary>
        [JsonPropertyName("CreatTime")]
        public long CreatTime { get; set; }

        /// <summary>
        /// 活跃天数
        /// </summary>
        [JsonPropertyName("ActiveDays")]
        public int ActiveDays { get; set; }

        /// <summary>
        /// 等级
        /// </summary>
        [JsonPropertyName("Level")]
        public int Level { get; set; }

        /// <summary>
        /// 索拉等级
        /// </summary>
        [JsonPropertyName("WorldLevel")]
        public int WorldLevel { get; set; }

        /// <summary>
        /// 角色数量
        /// </summary>
        [JsonPropertyName("RoleNum")]
        public int RoleNum { get; set; }

        /// <summary>
        /// 声匣数量
        /// </summary>
        [JsonPropertyName("SoundBox")]
        public int SoundBox { get; set; }

        /// <summary>
        /// 当前体力
        /// </summary>
        [JsonPropertyName("Energy")]
        public int Energy { get; set; }

        /// <summary>
        /// 体力上限
        /// </summary>
        [JsonPropertyName("MaxEnergy")]
        public int MaxEnergy { get; set; }

        /// <summary>
        /// 储备体力
        /// </summary>
        [JsonPropertyName("StoreEnergy")]
        public int StoreEnergy { get; set; }

        /// <summary>
        /// 储备体力恢复时间
        /// </summary>
        [JsonPropertyName("StoreEnergyRecoverTime")]
        public long StoreEnergyRecoverTime { get; set; }

        /// <summary>
        /// 储备体力上限
        /// </summary>
        [JsonPropertyName("MaxStoreEnergy")]
        public int MaxStoreEnergy { get; set; }

        /// <summary>
        /// 体力恢复时间
        /// </summary>
        [JsonPropertyName("EnergyRecoverTime")]
        public long EnergyRecoverTime { get; set; }

        /// <summary>
        /// 活跃度
        /// </summary>
        [JsonPropertyName("Liveness")]
        public int Liveness { get; set; }

        /// <summary>
        /// 活跃度上限
        /// </summary>
        [JsonPropertyName("LivenessMaxCount")]
        public int LivenessMaxCount { get; set; }

        /// <summary>
        /// 活跃度功能是否解锁
        /// </summary>
        [JsonPropertyName("LivenessUnlock")]
        public bool LivenessUnlock { get; set; }

        /// <summary>
        /// 章节 ID
        /// </summary>
        [JsonPropertyName("ChapterId")]
        public int ChapterId { get; set; }

        /// <summary>
        /// 周本次数
        /// </summary>
        [JsonPropertyName("WeeklyInstCount")]
        public int WeeklyInstCount { get; set; }

        /// <summary>
        /// 各类箱子数量统计
        /// </summary>
        [JsonPropertyName("Boxes")]
        public Dictionary<string, int>? Boxes { get; set; }

        /// <summary>
        /// 箱子统计
        /// </summary>
        [JsonPropertyName("BasicBoxes")]
        public Dictionary<string, int>? BasicBoxes { get; set; }

        /// <summary>
        /// 潮汐之遗
        /// </summary>
        [JsonPropertyName("PhantomBoxes")]
        public Dictionary<string, int>? PhantomBoxes { get; set; }

        /// <summary>
        /// 生日月份
        /// </summary>
        [JsonPropertyName("BirthMon")]
        public int BirthMon { get; set; }

        /// <summary>
        /// 生日日期
        /// </summary>
        [JsonPropertyName("BirthDay")]
        public int BirthDay { get; set; }

    }

    /// <summary>
    /// 先约电台
    /// </summary>
    public class RoleBattlePass
    {
        /// <summary>
        /// 电台等级
        /// </summary>
        [JsonPropertyName("Level")]
        public int Level { get; set; }

        /// <summary>
        /// 本周经验
        /// </summary>
        [JsonPropertyName("WeekExp")]
        public int WeekExp { get; set; }

        /// <summary>
        /// 本周经验上限
        /// </summary>
        [JsonPropertyName("WeekMaxExp")]
        public int WeekMaxExp { get; set; }

        /// <summary>
        /// 是否解锁
        /// </summary>
        [JsonPropertyName("IsUnlock")]
        public bool IsUnlock { get; set; }

        /// <summary>
        /// 是否开启
        /// </summary>
        [JsonPropertyName("IsOpen")]
        public bool IsOpen { get; set; }

        /// <summary>
        /// 当前经验
        /// </summary>
        [JsonPropertyName("Exp")]
        public int Exp { get; set; }

        /// <summary>
        /// 升级所需经验上限
        /// </summary>
        [JsonPropertyName("ExpLimit")]
        public int ExpLimit { get; set; }
    }

    /// <summary>
    /// 车载音乐
    /// </summary>
    public class RoleMusicData
    {
        /// <summary>
        /// 专辑编号
        /// </summary>
        [JsonPropertyName("Id")]
        public int Id { get; set; }

        /// <summary>
        /// 已收集数量
        /// </summary>
        [JsonPropertyName("Count")]
        public int Count { get; set; }

        /// <summary>
        /// 总数量
        /// </summary>
        [JsonPropertyName("TotalCount")]
        public int TotalCount { get; set; }

        public string IconPath => $"Local/Icons/{Id}.png";
    }

    /// <summary>
    /// 摩托数据
    /// </summary>
    public class RoleMotorData
    {
        /// <summary>
        /// 摩托等级
        /// </summary>
        [JsonPropertyName("Level")]
        public int Level { get; set; }

        /// <summary>
        /// 当前经验
        /// </summary>
        [JsonPropertyName("Exp")]
        public int Exp { get; set; }

        /// <summary>
        /// 下一级经验
        /// </summary>
        [JsonPropertyName("NextExp")]
        public int NextExp { get; set; }

        /// <summary>
        /// 皮肤列表
        /// </summary>
        [JsonPropertyName("Skins")]
        public List<MotorSkin>? Skins { get; set; }

        /// <summary>
        /// 贴纸列表
        /// </summary>
        [JsonPropertyName("Stickers")]
        public List<MotorSticker>? Stickers { get; set; }

        /// <summary>
        /// 装饰列表
        /// </summary>
        [JsonPropertyName("Decorations")]
        public List<MotorDecoration>? Decorations { get; set; }

        /// <summary>
        /// 车架列表
        /// </summary>
        [JsonPropertyName("Frames")]
        public List<MotorFrame>? Frames { get; set; }

        /// <summary>
        /// 当前装备皮肤
        /// </summary>
        [JsonPropertyName("EquipSkin")]
        public MotorSkin? EquipSkin { get; set; }
    }

    /// <summary>
    /// 摩托皮肤
    /// </summary>
    public class MotorSkin
    {
        /// <summary>
        /// 皮肤 ID
        /// </summary>
        [JsonPropertyName("SkinId")]
        public int SkinId { get; set; }

        /// <summary>
        /// 品质
        /// </summary>
        [JsonPropertyName("Quality")]
        public int Quality { get; set; }

        public string IconPath => $"Local/Icons/{SkinId}.png";
    }

    /// <summary>
    /// 摩托贴纸
    /// </summary>
    public class MotorSticker
    {
        /// <summary>
        /// 贴纸 ID
        /// </summary>
        [JsonPropertyName("Id")]
        public int Id { get; set; }

        /// <summary>
        /// 贴纸品质
        /// </summary>
        [JsonPropertyName("Quality")]
        public int Quality { get; set; }

        /// <summary>
        /// 部位编号
        /// </summary>
        [JsonPropertyName("PartId")]
        public int PartId { get; set; }

        public string IconPath => $"Local/Icons/{Id}.png";
        public string PartIconPath => $"StickerPart{PartId}Image";
    }

    /// <summary>
    /// 摩托装饰
    /// </summary>
    public class MotorDecoration
    {
        /// <summary>
        /// 装饰 ID
        /// </summary>
        [JsonPropertyName("Id")]
        public int Id { get; set; }

        /// <summary>
        /// 装饰品质
        /// </summary>
        [JsonPropertyName("Quality")]
        public int Quality { get; set; }

        /// <summary>
        /// 部位编号
        /// </summary>
        [JsonPropertyName("PartId")]
        public int PartId { get; set; }

        public string IconPath => $"Local/Icons/{Id}.png";

        public string PartIconPath => $"DecPart{PartId}Image";
    }

    /// <summary>
    /// 摩托车架
    /// </summary>
    public class MotorFrame
    {
        /// <summary>
        /// 车架 ID
        /// </summary>
        [JsonPropertyName("Id")]
        public int Id { get; set; }

        /// <summary>
        /// 车架品质
        /// </summary>
        [JsonPropertyName("Quality")]
        public int Quality { get; set; }
        public string IconPath => $"Local/Icons/{Id}.png";
    }
}