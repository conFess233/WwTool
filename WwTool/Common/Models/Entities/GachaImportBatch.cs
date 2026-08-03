namespace WwTool.Common.Models.Entities;

public sealed class GachaImportBatch
{
    public long Id { get; set; }
    public required string Uid { get; set; }
    public int PoolType { get; set; }
    public required string Source { get; set; }
    public DateTimeOffset StartedAtUtc { get; set; }
    public DateTimeOffset CompletedAtUtc { get; set; }
    public int RecordCount { get; set; }
    public long? FirstSourceOrder { get; set; }
    public long? LastSourceOrder { get; set; }
    public string? SourceCursor { get; set; }
    public UserAccount UserAccount { get; set; } = null!;
    public ICollection<GachaRecord> Records { get; set; } = [];
}
