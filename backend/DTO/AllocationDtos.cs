using System.ComponentModel.DataAnnotations;

namespace TemplateDormApi.DTO;

/// <summary>DORM-08 入住分配 — 契约 POST /allocations body</summary>
public class AllocationCreateDto
{
    /// <summary>契约字段 studentId；并发用例（IT-C2-002）允许省略并改为从登录态解析</summary>
    public string? StudentId { get; set; }

    [Required(ErrorMessage = "房间ID不能为空")]
    public int RoomId { get; set; }

    [Required(ErrorMessage = "床位号不能为空")]
    [Range(1, 99, ErrorMessage = "床位号必须在 1~99 之间")]
    public int BedNo { get; set; }

    [Required(ErrorMessage = "入住日期不能为空")]
    public DateTime CheckInDate { get; set; }
}

/// <summary>DORM-09 调寝 — 契约 POST /allocations/{allocationId}/transfer body（IT-C2-006）</summary>
public class AllocationTransferDto
{
    [Required(ErrorMessage = "目标房间ID不能为空")]
    public int TargetRoomId { get; set; }

    [Required(ErrorMessage = "目标床位号不能为空")]
    [Range(1, 99, ErrorMessage = "床位号必须在 1~99 之间")]
    public int TargetBedNo { get; set; }
}
