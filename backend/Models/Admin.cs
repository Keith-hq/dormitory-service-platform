namespace TemplateDormApi.Models;

public sealed class Admin
{
    public string AdminId { get; set; } = string.Empty;
    public string AdminName { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string RoleLevel { get; set; } = string.Empty;
    public long? BuildingId { get; set; }
}
