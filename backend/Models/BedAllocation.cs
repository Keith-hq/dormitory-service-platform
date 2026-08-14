namespace TemplateDormApi.Models;

/// <summary>
/// 学生床位分配记录，对应 D_BED_ALLOCATION。
/// </summary>
public sealed class BedAllocation
{
    public long AllocationId { get; set; }
    public string? StudentId { get; set; }
    public int? RoomId { get; set; }
    public int BedNo { get; set; }
    public DateTime CheckInDate { get; set; }
    public DateTime? CheckOutDate { get; set; }
}
