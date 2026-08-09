using Microsoft.AspNetCore.Mvc;
using TemplateDormApi.DTO;
using TemplateDormApi.Services;

namespace TemplateDormApi.Controllers;

/// <summary>
/// 房间管理 — DORM-04/05/06/07
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class RoomController : ControllerBase
{
    private readonly IRoomService _service;
    public RoomController(IRoomService service) => _service = service;

    /// <summary>DORM-04 房间列表</summary>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<object>>>> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] int? buildingId = null)
    {
        var result = await _service.GetPagedAsync(page, pageSize, buildingId);
        return Ok(ApiResponse.Ok(result));
    }

    /// <summary>DORM-04 根据 ID 查询房间详情</summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<ApiResponse<object>>> GetById(int id)
    {
        var room = await _service.GetByIdAsync(id);
        if (room == null) return NotFound(ApiResponse.Error(404, "房间不存在"));
        return Ok(ApiResponse.Ok(room));
    }

    /// <summary>DORM-05 新增房间</summary>
    [HttpPost]
    public async Task<ActionResult<ApiResponse<object>>> Create([FromBody] RoomCreateDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ApiResponse.Error(400, "参数校验失败"));
        var room = await _service.CreateAsync(dto);
        return Ok(ApiResponse.Created(room));
    }

    /// <summary>DORM-06 批量初始化房间</summary>
    [HttpPost("batch-init")]
    public ActionResult<ApiResponse<object>> BatchInit([FromBody] RoomBatchInitDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ApiResponse.Error(400, "参数校验失败"));
        // TODO: DORM-06 按楼层批量生成房间号
        return Ok(ApiResponse.Ok(new { }));
    }

    /// <summary>DORM-07 修改/停用房间</summary>
    [HttpPut("{id}")]
    public async Task<ActionResult<ApiResponse<object>>> Update(int id, [FromBody] RoomUpdateDto dto)
    {
        var room = await _service.UpdateAsync(id, dto);
        if (room == null) return NotFound(ApiResponse.Error(404, "房间不存在"));
        return Ok(ApiResponse.Ok(room, "更新成功"));
    }
}
