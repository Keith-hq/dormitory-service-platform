using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TemplateDormApi.Models;

/// <summary>
/// 楼栋实体
/// </summary>
public class Building
{
    [Key]
    public int BuildingId { get; set; }

    /// <summary>楼栋名称（如：1号楼）</summary>
    public string BuildingName { get; set; } = string.Empty;

    /// <summary>楼栋类型（男生宿舍 / 女生宿舍 / 混合宿舍）</summary>
    public string BuildingType { get; set; } = string.Empty;

    /// <summary>楼层数量</summary>
    public int FloorCount { get; set; }

    /// <summary>创建时间</summary>
    public DateTime CreateTime { get; set; } = DateTime.Now;
}
