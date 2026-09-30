namespace WwTool.Common.Models.Domain;

public sealed record GuidePlayerIdentity(long? PlayerId, string? ServerId);

public sealed class GuideWeaponData
{
    public string Uid { get; set; } = string.Empty;
    public string OwnerRoleGbId { get; set; } = string.Empty;
    public string WeaponGbId { get; set; } = string.Empty;
    public string? PictureUrl { get; set; }
    public int Star { get; set; }
    public int SourceOrder { get; set; }

}

public sealed class GuideRoleData
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

}
