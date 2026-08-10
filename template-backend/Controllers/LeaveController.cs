using Microsoft.AspNetCore.Mvc;
using TemplateDormApi.DTO;

namespace TemplateDormApi.Controllers;

/// <summary>
/// 离校报备 — COUN-01~04 / STU-15~17,40
/// 路由: /api/leave-applications (复数，对齐契约)
/// </summary>
[ApiController]
[Route("api/leave-applications")]
public class LeaveController : ControllerBase
{
    // ===== 辅导员端 =====

    /// <summary>COUN-01 待审批报备列表 — 契约 GET /leave-applications?status=</summary>
    [HttpGet]
    public ActionResult<ApiResponse<object>> List(
        [FromQuery] string? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        // TODO: 辅导员查看所有离校报备，按 status 筛选（待批/已通过/已驳回）
        return Ok(ApiResponse.Ok(new { items = Array.Empty<object>(), total = 0, page, pageSize }));
    }

    /// <summary>COUN-02 通过报备 — 契约 PUT /leave-applications/{applyId}/approve</summary>
    [HttpPut("{applyId}/approve")]
    public ActionResult<ApiResponse<object>> Approve(int applyId)
    {
        // TODO: 辅导员审批通过，状态→已批准
        return Ok(ApiResponse.Ok(new { }));
    }

    /// <summary>COUN-03 驳回报备 — 契约 PUT /leave-applications/{applyId}/reject</summary>
    [HttpPut("{applyId}/reject")]
    public ActionResult<ApiResponse<object>> Reject(int applyId, [FromBody] object dto)
    {
        // TODO: 辅导员驳回，reason 必填（DDL 暂无 Reason 列，C-023 待裁决）
        return Ok(ApiResponse.Ok(new { }));
    }

    /// <summary>COUN-04 离校统计 — 契约 GET /leave-applications/stats</summary>
    [HttpGet("stats")]
    public ActionResult<ApiResponse<object>> Statistics()
    {
        // TODO: 统计当前离校人数、目的地分布等
        return Ok(ApiResponse.Ok(new { }));
    }

    // ===== 学生端 =====

    /// <summary>STU-15 提交离校返校报备 — 契约 POST /leave-applications</summary>
    [HttpPost]
    public ActionResult<ApiResponse<object>> Submit([FromBody] object dto)
    {
        // TODO: 学生提交离校/返校申请，状态=待批
        return Ok(ApiResponse.Created(new { }));
    }

    /// <summary>STU-16 我的报备 — 契约 GET /students/{studentId}/leave-applications</summary>
    [HttpGet("/api/students/{studentId}/leave-applications")]
    public ActionResult<ApiResponse<object>> MyList(
        string studentId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        // TODO: 学生查看自己的报备记录，按 studentId 过滤
        return Ok(ApiResponse.Ok(new { items = Array.Empty<object>(), total = 0, page, pageSize }));
    }

    /// <summary>STU-17 修改报备 — 契约 PUT /leave-applications/{applyId}</summary>
    [HttpPut("{applyId}")]
    public ActionResult<ApiResponse<object>> Update(int applyId, [FromBody] object dto)
    {
        // TODO: 学生修改未审批的报备
        return Ok(ApiResponse.Ok(new { }));
    }

    /// <summary>STU-40 撤回报备 — 契约 POST /leave-applications/{applyId}/cancel</summary>
    [HttpPost("{applyId}/cancel")]
    public ActionResult<ApiResponse<object>> Cancel(int applyId)
    {
        // TODO: 学生撤回待审批的报备
        return Ok(ApiResponse.Ok(new { }));
    }
}
