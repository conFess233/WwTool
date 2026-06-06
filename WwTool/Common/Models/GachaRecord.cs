using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace WwTool.Common.Models
{
    // 配置联合索引
    [Index(nameof(Uid), nameof(PoolType), nameof(Time), IsDescending = new[] { false, false, true })]
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
        public int ResourceId { get; set; }
        public string? Name { get; set; }
        public string? ResourceType { get; set; }
        public int QualityLevel { get; set; }

        [Required]
        public string Time { get; set; } = null!;
    }
}
