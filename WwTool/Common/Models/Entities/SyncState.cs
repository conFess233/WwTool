namespace WwTool.Common.Models.Entities;

public sealed class SyncState
{
    public required string Uid { get; set; }
    public required string DataKind { get; set; }
    public string ScopeKey { get; set; } = string.Empty;
    public DateTimeOffset? LastSuccessfulSyncAtUtc { get; set; }
    public DateTimeOffset? SourceUpdatedAtUtc { get; set; }
    public string? LastCursor { get; set; }
    public UserAccount UserAccount { get; set; } = null!;
}
