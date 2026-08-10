using System.ComponentModel.DataAnnotations;

namespace TemplateDormApi.Models;

/// <summary>
/// 房间实体（映射 D_Room）
/// </summary>
public class Room
{
    [Key]
    public int RoomId { get; set; }

    /// <summary>所属楼栋ID（DDL 可空）</summary>
    public int? BuildingId { get; set; }

    /// <summary>房间编号（如：101）</summary>
    public string RoomNumber { get; set; } = string.Empty;

    /// <summary>最大容量（床位数，DDL 列 Capacity，可空）</summary>
    public int? Capacity { get; set; }

    /// <summary>当前入住人数（DDL 列 Occupancy，可空）</summary>
    public int? Occupancy { get; set; }

    /// <summary>供电状态（正常 / 断电）</summary>
    public string PowerStatus { get; set; } = "正常";
}
