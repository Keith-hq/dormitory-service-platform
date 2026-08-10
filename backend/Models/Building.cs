using System.ComponentModel.DataAnnotations;

namespace TemplateDormApi.Models;

/// <summary>
/// 楼栋实体（映射 D_Building）
/// </summary>
public class Building
{
    [Key]
    public int BuildingId { get; set; }

    /// <summary>楼栋名称（如：1号楼）</summary>
    public string BuildingName { get; set; } = string.Empty;

    /// <summary>楼栋类型（男生宿舍 / 女生宿舍 / 混合宿舍）</summary>
    public string BuildingType { get; set; } = string.Empty;

    /// <summary>楼层数量（DDL 列 Total_Floors，可空，无创建时间列）</summary>
    public int? FloorCount { get; set; }
}
