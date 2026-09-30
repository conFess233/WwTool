namespace WwTool.Common.Models.Domain;

/// <summary>角色或武器首次获得时间的最小读取结果。</summary>
public sealed record AcquisitionTimes(Dictionary<int, DateTime> Times, bool HasRecords);
