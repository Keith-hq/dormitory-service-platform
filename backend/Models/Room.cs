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

    /// <summary>楼层号（DDL 列 Floor，迁移 016 加列，可空）</summary>
    public int? Floor { get; set; }

    /// <summary>房间状态（正常 / 停用；DDL 列 Status，迁移 016 加列，DEFAULT '正常'，CK 校验）</summary>
    public string Status { get; set; } = "正常";

    /// <summary>供电状态（正常 / 断电）</summary>
    public string PowerStatus { get; set; } = "正常";
}
