using Microsoft.AspNetCore.Mvc;
using TemplateDormApi.DTO;
using TemplateDormApi.Services;

namespace TemplateDormApi.Controllers;

/// <summary>
/// 离校报备 — COUN-01~04 / STU-15~17,40
/// 路由: /api/leave-applications（对齐契约）
/// 状态机：待批 → 已通过/已驳回/已撤回；修改与撤销仅待批可用（IT-C2-007）。
/// </summary>
[ApiController]
[Route("api/leave-applications")]
public class LeaveController : ControllerBase
{
    private readonly ILeaveService _service;
    public LeaveController(ILeaveService service) => _service = service;

    // ===== 辅导员端 =====

    /// <summary>COUN-01 报备列表 — 契约 GET /leave-applications?status=（待批/已通过/已驳回/已撤回）</summary>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<object>>> List(
        [FromQuery] string? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        var result = await _service.GetPagedAsync(page, pageSize, status);
        return Ok(ApiResponse.Ok(result));
    }

    /// <summary>COUN-02 通过报备 — 契约 PUT /leave-applications/{applyId}/approve（仅待批）</summary>
    [HttpPut("{applyId}/approve")]
    public async Task<ActionResult<ApiResponse<object>>> Approve(int applyId)
    {
        var app = await _service.ApproveAsync(applyId);
        return Ok(ApiResponse.Ok(app, "审批通过"));
    }

    /// <summary>COUN-03 驳回报备 — 契约 PUT /leave-applications/{applyId}/reject（驳回必填原因）</summary>
    [HttpPut("{applyId}/reject")]
    public async Task<ActionResult<ApiResponse<object>>> Reject(int applyId, [FromBody] LeaveRejectDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ApiResponse.Error(400, "驳回原因不能为空"));
        var app = await _service.RejectAsync(applyId, dto.Reason);
        return Ok(ApiResponse.Ok(app, "已驳回"));
    }

    /// <summary>COUN-04 离校统计 — 契约 GET /leave-applications/stats</summary>
    [HttpGet("stats")]
    public async Task<ActionResult<ApiResponse<object>>> Statistics()
    {
        var stats = await _service.GetStatsAsync();
        return Ok(ApiResponse.Ok(stats));
    }

    // ===== 学生端 =====

    /// <summary>STU-15 提交离校返校报备 — 契约 POST /leave-applications（状态=待批）</summary>
    [HttpPost]
    public async Task<ActionResult<ApiResponse<object>>> Submit([FromBody] LeaveSubmitDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ApiResponse.Error(400, "参数校验失败"));
        var app = await _service.SubmitAsync(dto);
        return Ok(ApiResponse.Created(app));
    }

    /// <summary>STU-16 我的报备 — 契约 GET /students/{studentId}/leave-applications</summary>
    [HttpGet("/api/students/{studentId}/leave-applications")]
    public async Task<ActionResult<ApiResponse<object>>> MyList(
        string studentId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        var result = await _service.GetByStudentPagedAsync(studentId, page, pageSize);
        return Ok(ApiResponse.Ok(result));
    }

    /// <summary>STU-17 修改报备 — 契约 PUT /leave-applications/{applyId}（仅待批）</summary>
    [HttpPut("{applyId}")]
    public async Task<ActionResult<ApiResponse<object>>> Update(int applyId, [FromBody] LeaveUpdateDto dto)
    {
        var app = await _service.UpdateAsync(applyId, dto);
        return Ok(ApiResponse.Ok(app, "修改成功"));
    }

    /// <summary>STU-40 撤回报备 — 契约 POST /leave-applications/{applyId}/cancel（仅待批）</summary>
    [HttpPost("{applyId}/cancel")]
    public async Task<ActionResult<ApiResponse<object>>> Cancel(int applyId)
    {
        var app = await _service.CancelAsync(applyId);
        return Ok(ApiResponse.Ok(app, "已撤回"));
    }
}
