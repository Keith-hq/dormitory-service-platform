namespace DormBackendFacilityNotice.DTO;

/// <summary>
/// 公共设施新增 DTO（骨架，字段待对照契约补全）
/// </summary>
public class FacilityCreateDto
{
    public int BuildingId { get; set; }
    public string FacilityCode { get; set; } = string.Empty;
    public string FacilityType { get; set; } = string.Empty;
}

/// <summary>
/// 公共设施编辑 DTO（骨架）
/// </summary>
public class FacilityUpdateDto
{
    public string? FacilityCode { get; set; }
    public string? FacilityType { get; set; }
}
