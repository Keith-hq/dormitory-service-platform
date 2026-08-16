using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TemplateDormApi.DTO;
using TemplateDormApi.Security;
using TemplateDormApi.Services;

namespace TemplateDormApi.Controllers;

/// <summary>
/// 住宿分配 — DORM-08 入住（并发唯一）/ DORM-09 调寝 / DORM-10 房间住户
/// 鉴权：入住为自助流程（IT-C2-002 学生 token，studentId 缺省从登录态解析）；
/// 调寝/住户查询为宿管端（DORM-09/10）。
/// </summary>
[ApiController]
public class BedAllocationController : ControllerBase
{
    private readonly IAllocationService _service;
    public BedAllocationController(IAllocationService service) => _service = service;

    /// <summary>
    /// DORM-08 入住分配 — 契约 POST /allocations {studentId, roomId, bedNo, checkInDate}
    /// 床位并发唯一由 UK_D_BED_ALLOC_ACTIVE 兜底；studentId 缺省时（IT-C2-002）从登录态解析。
    /// </summary>
    [HttpPost("api/allocations")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<object>>> Create([FromBody] AllocationCreateDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ApiResponse.Error(400, "参数校验失败"));
        var alloc = await _service.CreateAsync(dto, CurrentUser.GetAccountId(User));
        return Ok(ApiResponse.Created(alloc));
    }

    /// <summary>DORM-09 调寝 — 契约 POST /allocations/{allocationId}/transfer {targetRoomId, targetBedNo}</summary>
    [HttpPost("api/allocations/{allocationId}/transfer")]
    [Authorize(Policy = AuthPolicies.DormAdmin)]
    public async Task<ActionResult<ApiResponse<object>>> Transfer(int allocationId, [FromBody] AllocationTransferDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ApiResponse.Error(400, "参数校验失败"));
        var alloc = await _service.TransferAsync(allocationId, dto);
        return Ok(ApiResponse.Ok(alloc, "调寝成功"));
    }

    /// <summary>DORM-10 房间住户 — 契约 GET /rooms/{roomId}/occupants</summary>
    [HttpGet("api/rooms/{roomId}/occupants")]
    [Authorize(Policy = AuthPolicies.DormAdmin)]
    public async Task<ActionResult<ApiResponse<object>>> Occupants(int roomId)
    {
        var result = await _service.GetOccupantsAsync(roomId);
        return Ok(ApiResponse.Ok(result));
    }
}
