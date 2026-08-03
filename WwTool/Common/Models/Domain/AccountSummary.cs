namespace WwTool.Common.Models.Domain;

public sealed class AccountSummary
{
    public required string Uid { get; init; }
    public string? Region { get; init; }
    public string? Name { get; init; }
    public int Level { get; init; }
    public int Sex { get; init; }
    public int HeadPhoto { get; init; }
    public DateTimeOffset? LastSyncedAtUtc { get; init; }
    public string IconPath => $"Local/Icons/{HeadPhoto}.png";
}
