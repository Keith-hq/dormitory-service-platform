using Microsoft.AspNetCore.Mvc;
using TemplateDormApi.DTO;
using TemplateDormApi.Services;

namespace TemplateDormApi.Controllers;

/// <summary>
/// 房间管理 — DORM-04/05/06/07
/// 路由: /api/rooms (复数，对齐契约)
/// </summary>
[ApiController]
[Route("api/rooms")]
public class RoomController : ControllerBase
{
    private readonly IRoomService _service;
    public RoomController(IRoomService service) => _service = service;

    /// <summary>DORM-04 按楼栋查房间列表 — 契约 GET /buildings/{buildingId}/rooms</summary>
    [HttpGet("/api/buildings/{buildingId}/rooms")]
    public async Task<ActionResult<ApiResponse<PagedResult<object>>>> ListByBuilding(
        int buildingId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        var result = await _service.GetPagedAsync(page, pageSize, buildingId);
        return Ok(ApiResponse.Ok(result));
    }

    /// <summary>DORM-04 房间详情 — 契约 GET /rooms/{roomId}</summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<ApiResponse<object>>> GetById(int id)
    {
        var room = await _service.GetByIdAsync(id);
        if (room == null) return NotFound(ApiResponse.Error(404, "房间不存在"));
        return Ok(ApiResponse.Ok(room));
    }

    /// <summary>DORM-05 新增房间 — 契约 POST /rooms</summary>
    [HttpPost]
    public async Task<ActionResult<ApiResponse<object>>> Create([FromBody] RoomCreateDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ApiResponse.Error(400, "参数校验失败"));
        var room = await _service.CreateAsync(dto);
        return Ok(ApiResponse.Created(room));
    }

    /// <summary>DORM-06 批量初始化房间 — 契约 POST /rooms/batch-init（幂等）</summary>
    [HttpPost("batch-init")]
    public ActionResult<ApiResponse<object>> BatchInit(
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        [FromBody] RoomBatchInitDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ApiResponse.Error(400, "参数校验失败"));
        // TODO: 幂等检查 idempotencyKey → Redis/DB 去重
        // TODO: 按 floor/startRoomNo/count 生成房间
        return Ok(ApiResponse.Ok(new { idempotencyKey }));
    }

    /// <summary>DORM-07 修改/停用房间 — 契约 PUT /rooms/{roomId}</summary>
    [HttpPut("{id}")]
    public async Task<ActionResult<ApiResponse<object>>> Update(int id, [FromBody] RoomUpdateDto dto)
    {
        var room = await _service.UpdateAsync(id, dto);
        if (room == null) return NotFound(ApiResponse.Error(404, "房间不存在"));
        return Ok(ApiResponse.Ok(room, "更新成功"));
    }
}
