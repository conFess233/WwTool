namespace WwTool.Core.Models.Domain;

public sealed class AccountSummary
{
    public required string Uid { get; init; }
    public string? Region { get; init; }
    public string? Name { get; init; }
    public int Level { get; init; }
    public int Sex { get; init; }
    public int HeadPhoto { get; init; }
    public DateTimeOffset? LastSyncedAtUtc { get; init; }
    /// <summary>界面上的脱敏 UID：保留前 4 位，其余为圆点（如 7125●●●●●）。</summary>
    public string MaskedUid => Uid.Length > 4 ? Uid[..4] + new string('●', Uid.Length - 4) : Uid;
    public string IconPath => $"Local/Icons/{HeadPhoto}.png";
}
