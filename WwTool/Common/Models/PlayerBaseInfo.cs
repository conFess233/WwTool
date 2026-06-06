using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WwTool.Common.Models
{
    /// <summary>
    /// 玩家基础信息数据库模型
    /// </summary>
    public class PlayerBaseInfo
    {
        [Key]
        public string Uid { get; set; } = null!;

        public string RoleName { get; set; } = string.Empty;
        public int Level { get; set; }
        public int WorldLevel { get; set; }
        public int ActiveDays { get; set; }
        public int RoleNum { get; set; }
        public int SoundBox { get; set; }
        public int Energy { get; set; }
        public int MaxEnergy { get; set; }
        public int StoreEnergy { get; set; }
        public int MaxStoreEnergy { get; set; }
        public int Liveness { get; set; }
        public int LivenessMaxCount { get; set; }
        public bool LivenessUnlock { get; set; }
        public int WeeklyInstCount { get; set; }
        public long CreatTime { get; set; }
        public int BirthMon { get; set; }
        public int BirthDay { get; set; }
        public long StoreEnergyRecoverTime { get; set; }
        public long EnergyRecoverTime { get; set; }

        public string BoxesJson { get; set; } = "{}";
        public string BasicBoxesJson { get; set; } = "{}";
        public string PhantomBoxesJson { get; set; } = "{}";

        [ForeignKey("Uid")]
        public virtual UserAccount? UserAccount { get; set; }
    }
}
