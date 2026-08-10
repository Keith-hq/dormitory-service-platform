using System.ComponentModel.DataAnnotations;

namespace TemplateDormApi.DTO;

/// <summary>DORM-05 新增房间 — 契约 POST /rooms body</summary>
public class RoomCreateDto
{
    [Required] public int BuildingId { get; set; }

    /// <summary>房间号 — 契约 roomNo</summary>
    [Required] public string RoomNo { get; set; } = string.Empty;

    /// <summary>楼层 — 契约 floor (必填；迁移 016 已加列 D_Room.Floor)</summary>
    [Required] public int Floor { get; set; }

    [Range(1, 10)] public int Capacity { get; set; } = 4;
}

/// <summary>DORM-07 修改/停用房间 — 契约 PUT /rooms/{roomId} body</summary>
public class RoomUpdateDto
{
    /// <summary>房间号 — 契约 roomNo</summary>
    public string? RoomNo { get; set; }

    public int? Capacity { get; set; }

    /// <summary>房间状态 — 契约 status (正常/停用；迁移 016 已加列 D_Room.Status)</summary>
    [RegularExpression("^(正常|停用)$", ErrorMessage = "状态只能为 正常 或 停用")]
    public string? Status { get; set; }
}

/// <summary>DORM-06 批量初始化 — 契约 POST /rooms/batch-init body（幂等）</summary>
public class RoomBatchInitDto
{
    [Required] public int BuildingId { get; set; }

    /// <summary>楼层 — 契约 floor</summary>
    [Required] public int Floor { get; set; }

    /// <summary>起始房间号 — 契约 startRoomNo</summary>
    [Required] public string StartRoomNo { get; set; } = string.Empty;

    /// <summary>生成数量 — 契约 count</summary>
    [Required, Range(1, 100)] public int Count { get; set; }

    [Required, Range(1, 10)] public int Capacity { get; set; } = 4;
}
