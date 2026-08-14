using System.ComponentModel.DataAnnotations;

namespace TemplateDormApi.Models;

/// <summary>
/// 住宿分配实体（映射 D_Bed_Allocation）。
/// 床位表唯一数据拥有者为住宿模块，本实体仅用于「按 CheckOut_Date 为空」只读查询当前学生房间，
/// 不参与任何写操作。
/// </summary>
public class BedAllocation
{
    [Key]
    public int AllocationId { get; set; }

    /// <summary>入住学生（FK → D_Student，可空）</summary>
    public string? StudentId { get; set; }

    /// <summary>入住房间（FK → D_Room，可空）</summary>
    public int? RoomId { get; set; }

    /// <summary>床位号</summary>
    public int BedNo { get; set; }

    /// <summary>入住日期</summary>
    public DateTime CheckInDate { get; set; }

    /// <summary>退宿日期（NULL=在住）</summary>
    public DateTime? CheckOutDate { get; set; }
}
