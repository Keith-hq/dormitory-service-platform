using Microsoft.AspNetCore.Mvc;
using TemplateDormApi.DTO;

namespace TemplateDormApi.Controllers;

/// <summary>
/// 住宿分配 — DORM-08/09/10
/// 路由: /api/allocations / /api/rooms (复数，对齐契约)
/// </summary>
[ApiController]
public class BedAllocationController : ControllerBase
{
    /// <summary>DORM-08 入住分配 — 契约 POST /allocations</summary>
    [HttpPost("api/allocations")]
    public ActionResult<ApiResponse<object>> Create([FromBody] object dto)
    {
        // TODO: {studentId, roomId, bedNo, checkInDate} 分配学生到房间床位
        // D_Room.Occupancy +1（事务）；床位并发唯一 ⇒ UK_D_BED_ALLOC_ACTIVE
        return Ok(ApiResponse.Created(new { }));
    }

    /// <summary>DORM-09 调寝 — 契约 POST /allocations/{allocationId}/transfer</summary>
    [HttpPost("api/allocations/{allocationId}/transfer")]
    public ActionResult<ApiResponse<object>> Transfer(int allocationId, [FromBody] object dto)
    {
        // TODO: 写旧记录 CheckOut_Date，建新记录，联动两个房间 Occupancy
        return Ok(ApiResponse.Ok(new { }));
    }

    /// <summary>DORM-10 房间住户 — 契约 GET /rooms/{roomId}/occupants</summary>
    [HttpGet("api/rooms/{roomId}/occupants")]
    public ActionResult<ApiResponse<object>> Occupants(int roomId)
    {
        // TODO: 查询某房间当前所有在住学生信息
        return Ok(ApiResponse.Ok(new { items = Array.Empty<object>() }));
    }
}
