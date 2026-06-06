using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Text.Json.Serialization;

namespace WwTool.Common.Models
{
    /// <summary>
    /// 用户账号数据库模型
    /// </summary>
    public class UserAccount : BaseModel
    {
        /// <summary>
        /// 用户 UID
        /// </summary>
        [Key]
        public string Uid { get; set; } = null!;

        /// <summary>
        /// 加密的授权码
        /// </summary>
        public string? EncryptedOauthCode { get; set; }

        public string? Region { get; set; } = string.Empty;

        public string? Name { get; set; } = string.Empty;

        public int Level { get; set; } = 0;

        public int Sex { get; set; } = 0;

        public int HeadPhoto { get; set; } = 0;
        public string IconPath => $"Local/Icons/{HeadPhoto}.png";

        public ICollection<GachaRecord> GachaRecords { get; set; } = new List<GachaRecord>();
    }
}
