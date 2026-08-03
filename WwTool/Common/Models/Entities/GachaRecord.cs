using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace WwTool.Common.Models.Entities
{
    // 配置联合索引
    public class GachaRecord
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        public string Uid { get; set; } = null!;

        // 导航属性
        [ForeignKey(nameof(Uid))]
        public UserAccount UserAccount { get; set; } = null!;

        public int PoolType { get; set; }
        public long ImportBatchId { get; set; }
        public GachaImportBatch ImportBatch { get; set; } = null!;
        public int ResourceId { get; set; }
        public string? NameSnapshot { get; set; }
        [NotMapped]
        public string? Name { get => NameSnapshot; set => NameSnapshot = value; }
        public string? ResourceType { get; set; }
        public int QualityLevel { get; set; }

        [Required]
        public string Time { get; set; } = null!;
        public string? SourceRecordId { get; set; }
        public DateTimeOffset? SourceOccurredAtUtc { get; set; }
        public int ApiPageIndex { get; set; }
        public int ResponseItemIndex { get; set; }
        public long SourceOrder { get; set; }
        public int DuplicateOccurrenceIndex { get; set; }
        public string StableFingerprint { get; set; } = string.Empty;
        public DateTimeOffset ImportedAtUtc { get; set; }
    }
}
