using System.ComponentModel.DataAnnotations;

namespace DormBackendFacilityNotice.DTO;

/// <summary>
/// 公共设施新增 DTO（对齐契约 POST /facilities）
/// </summary>
public class FacilityCreateDto
{
    [Range(1, int.MaxValue, ErrorMessage = "所属楼栋不能为空")]
    public int BuildingId { get; set; }

    [Required(ErrorMessage = "设施编号不能为空")]
    public string FacilityCode { get; set; } = string.Empty;

    [Required(ErrorMessage = "设施类型不能为空")]
    public string FacilityType { get; set; } = string.Empty;

    [RegularExpression("^(正常|维修|停用)$", ErrorMessage = "状态只能是：正常 / 维修 / 停用")]
    public string Status { get; set; } = "正常";
}

/// <summary>
/// 公共设施编辑 DTO（骨架，后续 PUT 用）
/// </summary>
public class FacilityUpdateDto
{
    public string? FacilityCode { get; set; }
    public string? FacilityType { get; set; }
    public string? Status { get; set; }
}
