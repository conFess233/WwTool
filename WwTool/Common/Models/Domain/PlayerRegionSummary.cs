namespace WwTool.Common.Models.Domain;

public sealed class PlayerRegionSummary
{
    public string RoleId { get; set; } = string.Empty;
    public string RoleName { get; set; } = string.Empty;
    public int Level { get; set; }
    public int Sex { get; set; }
    public int HeadPhoto { get; set; }
}
