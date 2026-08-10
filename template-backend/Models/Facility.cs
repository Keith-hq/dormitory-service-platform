using System.ComponentModel.DataAnnotations;

namespace TemplateDormApi.Models;

/// <summary>
/// 公共设施实体（映射 D_Facility，洗衣机/浴室等）
/// </summary>
public class Facility
{
    [Key]
    public int FacilityId { get; set; }

    /// <summary>所属楼栋（FK → D_Building）</summary>
    public int BuildingId { get; set; }

    /// <summary>设施编号（如 WASHER-01）</summary>
    public string FacilityCode { get; set; } = string.Empty;

    /// <summary>设施类型（洗衣机 / 浴室 / ...）</summary>
    public string FacilityType { get; set; } = string.Empty;

    /// <summary>状态（正常 / 维修 / 停用）</summary>
    public string Status { get; set; } = "正常";
}
