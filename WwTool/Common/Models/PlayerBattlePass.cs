using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WwTool.Common.Models
{
    public class PlayerBattlePass
    {
        [Key]
        public string Uid { get; set; } = null!;

        public int Level { get; set; }
        public int WeekExp { get; set; }
        public int WeekMaxExp { get; set; }
        public bool IsUnlock { get; set; }
        public bool IsOpen { get; set; }
        public int Exp { get; set; }
        public int ExpLimit { get; set; }

        [ForeignKey("Uid")]
        public virtual UserAccount? UserAccount { get; set; }
    }
}
