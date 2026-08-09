using Microsoft.AspNetCore.Mvc;
using TemplateDormApi.DTO;

namespace TemplateDormApi.Controllers;

/// <summary>
/// 离校报备 — COUN-01~04 / STU-15~17,40
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class LeaveController : ControllerBase
{
    // ===== 辅导员端 =====

    /// <summary>COUN-01 待审批报备列表</summary>
    [HttpGet("pending")]
    public ActionResult<ApiResponse<object>> PendingList(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        // TODO: 辅导员查看所有待审批的离校报备
        return Ok(ApiResponse.Ok(new { items = Array.Empty<object>(), total = 0, page, pageSize }));
    }

    /// <summary>COUN-02 通过报备</summary>
    [HttpPost("{id}/approve")]
    public ActionResult<ApiResponse<object>> Approve(int id)
    {
        // TODO: 辅导员审批通过，状态→已批准
        return Ok(ApiResponse.Ok(new { }));
    }

    /// <summary>COUN-03 驳回报备</summary>
    [HttpPost("{id}/reject")]
    public ActionResult<ApiResponse<object>> Reject(int id, [FromBody] object dto)
    {
        // TODO: 辅导员驳回，需填写驳回原因
        return Ok(ApiResponse.Ok(new { }));
    }

    /// <summary>COUN-04 离校统计【新增】</summary>
    [HttpGet("statistics")]
    public ActionResult<ApiResponse<object>> Statistics()
    {
        // TODO: 统计当前离校人数、目的地分布等
        return Ok(ApiResponse.Ok(new { }));
    }

    // ===== 学生端 =====

    /// <summary>STU-15 提交离校返校报备</summary>
    [HttpPost]
    public ActionResult<ApiResponse<object>> Submit([FromBody] object dto)
    {
        // TODO: 学生提交离校/返校申请，状态=待批
        return Ok(ApiResponse.Created(new { }));
    }

    /// <summary>STU-16 我的报备</summary>
    [HttpGet("my")]
    public ActionResult<ApiResponse<object>> MyList(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        // TODO: 学生查看自己的报备记录
        return Ok(ApiResponse.Ok(new { items = Array.Empty<object>(), total = 0, page, pageSize }));
    }

    /// <summary>STU-17 修改报备</summary>
    [HttpPut("{id}")]
    public ActionResult<ApiResponse<object>> Update(int id, [FromBody] object dto)
    {
        // TODO: 学生修改未审批的报备
        return Ok(ApiResponse.Ok(new { }));
    }

    /// <summary>STU-40 撤回报备</summary>
    [HttpPost("{id}/withdraw")]
    public ActionResult<ApiResponse<object>> Withdraw(int id)
    {
        // TODO: 学生撤回待审批的报备
        return Ok(ApiResponse.Ok(new { }));
    }
}
