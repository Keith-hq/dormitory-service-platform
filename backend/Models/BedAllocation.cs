namespace TemplateDormApi.Models;

/// <summary>
/// 住宿分配实体（映射 D_Bed_Allocation）
/// 刘润东为 D_Allocation/床位唯一数据拥有者：入住/调寝/退宿/清算全流程读写均在本模块。
/// 主键无序列（DDL 冻结不改），由应用层 MAX+1 生成，见 BedAllocationRepository.NextAllocationIdAsync。
/// 并发保证：UK_D_BED_ALLOC_ACTIVE（房间+在住床位唯一）；CheckOut_Date 作并发令牌，
/// 同一分配只能被一个事务写入退宿日期（调寝并发"仅一次生效"、退宿幂等均依赖此）。
/// </summary>
public class BedAllocation
{
    /// <summary>分配ID（ALLOCATION_ID）</summary>
    public int AllocationId { get; set; }

    /// <summary>学生学号（STUDENT_ID，FK → D_Student）</summary>
    public string? StudentId { get; set; }

    /// <summary>房间ID（ROOM_ID，FK → D_Room）</summary>
    public int? RoomId { get; set; }

    /// <summary>床位号（BED_NO，≥1）</summary>
    public int BedNo { get; set; }

    /// <summary>入住日期（CHECK_IN_DATE）</summary>
    public DateTime CheckInDate { get; set; }

    /// <summary>退宿日期（CHECK_OUT_DATE）；NULL = 在住</summary>
    public DateTime? CheckOutDate { get; set; }
}
