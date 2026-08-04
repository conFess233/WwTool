namespace WwTool.Common.Models.Entities;

public sealed class GuideAccountCredential
{
    public string CUid { get; set; } = string.Empty;
    public string EncryptedGuideToken { get; set; } = string.Empty;
    public ICollection<GuidePlayerSnapshot> Players { get; set; } = [];
}

public sealed class GuidePlayerSnapshot
{
    public string Uid { get; set; } = string.Empty;
    public string CUid { get; set; } = string.Empty;
    public string ServerId { get; set; } = string.Empty;
    public DateTimeOffset? LastSyncedAtUtc { get; set; }
    public UserAccount UserAccount { get; set; } = null!;
    public GuideAccountCredential Credential { get; set; } = null!;
    public ICollection<GuideRoleSnapshot> Roles { get; set; } = [];
    public ICollection<GuideEquippedWeaponSnapshot> Weapons { get; set; } = [];
}

public sealed class GuideRoleSnapshot
{
    public string Uid { get; set; } = string.Empty;
    public string RoleGbId { get; set; } = string.Empty;
    public int SourceOrder { get; set; }
    public string? CardPictureUrl { get; set; }
    public string? IllustrationPictureUrl { get; set; }
    public int Star { get; set; }
    public int RoleStatus { get; set; }
    public int Sequence { get; set; }
    public bool IsAcquired { get; set; }
    public string? MayRoleGbId { get; set; }
    public long? StrategyId { get; set; }
    public long? StrategyModifiedAt { get; set; }
    public string? DetailJson { get; set; }
    public GuidePlayerSnapshot Player { get; set; } = null!;
}

public sealed class GuideEquippedWeaponSnapshot
{
    public string Uid { get; set; } = string.Empty;
    public string OwnerRoleGbId { get; set; } = string.Empty;
    public string WeaponGbId { get; set; } = string.Empty;
    public string? PictureUrl { get; set; }
    public int Star { get; set; }
    public int SourceOrder { get; set; }
    public GuidePlayerSnapshot Player { get; set; } = null!;
}
