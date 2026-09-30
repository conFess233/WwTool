namespace WwTool.Common.Models.Domain;

/// <summary>单类资料的同步结果，状态键由 UI 本地化。</summary>
public sealed record CatalogSyncStatus(string Category, string StatusKey, CatalogVersion Version, int Count,
    DateTimeOffset? LastSuccessUtc = null, int Added = 0, int Missing = 0);
