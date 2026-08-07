using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TemplateDormApi.Models;

/// <summary>
/// 房间实体
/// </summary>
public class Room
{
    [Key]
    public int RoomId { get; set; }

    /// <summary>所属楼栋ID</summary>
    public int BuildingId { get; set; }

    /// <summary>房间编号（如：101）</summary>
    public string RoomNumber { get; set; } = string.Empty;

    /// <summary>最大容量（床位数）</summary>
    public int Capacity { get; set; }

    /// <summary>已入住人数</summary>
    public int Occupied { get; set; }

    /// <summary>房间状态（空闲 / 已满 / 维修中）</summary>
    public string Status { get; set; } = "空闲";

    /// <summary>供电状态（正常 / 断电）</summary>
    [Column("Power_Status")]
    public string PowerStatus { get; set; } = "正常";
}
