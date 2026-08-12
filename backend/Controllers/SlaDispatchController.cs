using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using TemplateDormApi.Data;
using TemplateDormApi.DTO;
using TemplateDormApi.Exceptions;
using TemplateDormApi.Security;
using TemplateDormApi.Services;

namespace TemplateDormApi.Controllers;

/// <summary>
/// SLA 派单接口（难点⑤ 一审修复版）。
/// DORM-26~28 宿管端报修派单。
/// </summary>
[ApiController]
[Route("api")]
public class SlaDispatchController : ControllerBase
{
    private readonly ISlaDispatchService _service;
    private readonly AppDbContext _context;

    public SlaDispatchController(ISlaDispatchService service, AppDbContext context)
    {
        _service = service;
        _context = context;
    }

    /// <summary>从 JWT 解析当前管理员的 Admin_ID</summary>
    private async Task<string> ResolveAdminId()
    {
        var accountId = CurrentUser.GetAccountId(User)
            ?? throw new BusinessException(401, "未登录或 Token 无效");

        var adminId = await _context.UserAccounts
            .Where(a => a.AccountId == accountId)
            .Select(a => a.AdminId)
            .FirstOrDefaultAsync();

        return adminId ?? throw new BusinessException(401, "当前账户未关联管理员身份");
    }

    // ==================== 宿管端 ====================

    /// <summary>DORM-26：待处理工单列表（分页）</summary>
    [HttpGet("admins/{adminId}/repair-tickets")]
    [Authorize(Policy = AuthPolicies.DormAdmin)]
    public async Task<ActionResult<ApiResponse<PagedResult<object>>>> GetPendingTickets(
        string adminId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        // adminId 来自 URL，需与 JWT 解析的管理员身份一致
        var currentAdminId = await ResolveAdminId();
        if (!string.Equals(adminId, currentAdminId, StringComparison.Ordinal))
            return Ok(ApiResponse.Error(403, "无权访问其他管理员的工单"));

        if (page < 1 || pageSize < 1 || pageSize > 100)
            return Ok(ApiResponse.Error(400, "分页参数不合法：page>=1，1<=pageSize<=100"));

        var result = await _service.GetPendingTickets(adminId, page, pageSize);
        return Ok(ApiResponse.Ok(result));
    }

    /// <summary>DORM-27：接单（并发唯一，仅允许被指派的维修员接单）</summary>
    [HttpPost("repair-tickets/{ticketId}/claim")]
    [Authorize(Policy = AuthPolicies.DormAdmin)]
    public async Task<ActionResult<ApiResponse<object>>> Claim(int ticketId)
    {
        var adminId = await ResolveAdminId();
        var rc = await _service.ClaimTicket(ticketId, adminId);

        return rc == 0
            ? Ok(ApiResponse.Ok(new { }, "接单成功"))
            : Ok(ApiResponse.Error(400, "工单不存在、状态不是待处理或非本人指派"));
    }

    /// <summary>DORM-28：录入维修日志（完工）</summary>
    [HttpPost("repair-tickets/{ticketId}/logs")]
    [Authorize(Policy = AuthPolicies.DormAdmin)]
    public async Task<ActionResult<ApiResponse<object>>> Complete(
        int ticketId, [FromBody] CompleteRepairRequest req)
    {
        var adminId = await ResolveAdminId();
        var rc = await _service.CompleteRepair(
            ticketId, adminId, req.Content, req.Result, req.SolveTime);

        var msgs = new[] { "维修完成", "工单不存在", "状态不是处理中或非本人操作", "维修日志已存在" };
        var msg = rc >= 0 && rc < msgs.Length ? msgs[rc] : "未知错误";
        return rc == 0
            ? Ok(ApiResponse.Ok(new { }, msg))
            : Ok(ApiResponse.Error(400, msg));
    }

    // ==================== Internal ====================

    /// <summary>自动派单（Internal + ServiceKeyAuth）</summary>
    [HttpPost("internal/scheduler/assign-ticket")]
    [ServiceKeyAuth]
    public async Task<ActionResult<ApiResponse<object>>> AssignTicket(
        [FromQuery] int ticketId)
    {
        var rc = await _service.AssignTicket(ticketId);
        return Ok(ApiResponse.Ok(new { resultCode = rc }, "派单完成"));
    }

    /// <summary>SLA 升级巡检（Internal + ServiceKeyAuth）</summary>
    [HttpPost("internal/scheduler/escalate-sla")]
    [ServiceKeyAuth]
    public async Task<ActionResult<ApiResponse<object>>> EscalateSla()
    {
        await _service.EscalateSla();
        return Ok(ApiResponse.Ok(new { }, "SLA升级巡检完成"));
    }
}

/// <summary>完工请求体（DORM-28）</summary>
public class CompleteRepairRequest
{
    /// <summary>维修处理描述</summary>
    [Required(ErrorMessage = "维修描述不能为空")]
    [StringLength(500, ErrorMessage = "维修描述最长500字")]
    public string Content { get; set; } = string.Empty;

    /// <summary>维修结果（可选）</summary>
    [StringLength(200, ErrorMessage = "维修结果最长200字")]
    public string? Result { get; set; }

    /// <summary>解决时间（可选，默认当前时间）</summary>
    public DateTime? SolveTime { get; set; }
}
