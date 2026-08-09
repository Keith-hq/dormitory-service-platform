using System.ComponentModel.DataAnnotations;

namespace TemplateDormApi.DTO;

/// <summary>DORM-05 新增房间</summary>
public class RoomCreateDto
{
    [Required] public int BuildingId { get; set; }
    [Required] public string RoomNumber { get; set; } = string.Empty;
    [Range(1, 10)] public int Capacity { get; set; } = 4;
}

/// <summary>DORM-07 修改/停用房间</summary>
public class RoomUpdateDto
{
    public string? RoomNumber { get; set; }
    public int? Capacity { get; set; }
    public string? Status { get; set; }
    public string? PowerStatus { get; set; }
}

/// <summary>DORM-06 批量初始化</summary>
public class RoomBatchInitDto
{
    [Required] public int BuildingId { get; set; }
    [Required] public int StartFloor { get; set; }
    [Required] public int EndFloor { get; set; }
    [Required] public int RoomsPerFloor { get; set; }
    [Required] public int Capacity { get; set; } = 4;
}
