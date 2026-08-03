using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WwTool.Common.Models.Entities
{
    public class PlayerMusicData
    {
        public string Uid { get; set; } = null!;
        public int AlbumId { get; set; }
        public int Count { get; set; }
        public int TotalCount { get; set; }
        public DateTimeOffset? LastSyncedAtUtc { get; set; }

        public string IconPath
        {
            get
            {
                return $"Local/Icons/{AlbumId}.png";
            }
        }

        [ForeignKey("Uid")]
        public virtual UserAccount? UserAccount { get; set; }
    }
}
