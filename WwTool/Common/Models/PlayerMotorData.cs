using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WwTool.Common.Models
{
    public class PlayerMotorData
    {
        [Key]
        public string Uid { get; set; } = null!;

        public int Level { get; set; }
        public int Exp { get; set; }
        public int NextExp { get; set; }

        public string SkinsJson { get; set; } = "[]";
        public string StickersJson { get; set; } = "[]";
        public string DecorationsJson { get; set; } = "[]";
        public string FramesJson { get; set; } = "[]";

        public int EquipSkinId { get; set; }
        public int EquipSkinQuality { get; set; }

        [ForeignKey("Uid")]
        public virtual UserAccount? UserAccount { get; set; }
    }
}
