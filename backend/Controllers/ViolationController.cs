using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TemplateDormApi.Data;
using TemplateDormApi.DTO;
using TemplateDormApi.Security;
using TemplateDormApi.Services;

namespace TemplateDormApi.Controllers;

[ApiController]
[Authorize(Policy = AuthPolicies.DormAdmin)]
[Route("api/violations")]
public sealed class ViolationController : ControllerBase
{
    private readonly IViolationService _service;
    private readonly AppDbContext _context;
    private readonly IAuditService _auditService;
    private readonly ILogger<ViolationController> _logger;

    public ViolationController(
        IViolationService service,
        AppDbContext context,
        IAuditService auditService,
        ILogger<ViolationController> logger)
    {
        _service = service;
        _context = context;
        _auditService = auditService;
        _logger = logger;
    }

    /// <summary>VIOL-01 登记违规违纪。</summary>
    [HttpPost]
    public async Task<ActionResult<ApiResponse<ViolationDto>>> Create(
        [FromBody] CreateViolationRequest request,
        CancellationToken cancellationToken)
    {
        // Record_By 取当前管理员登录名（ClaimTypes.Name），标记登记人
        var recordBy = User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value;
        var result = await _service.CreateAsync(request, recordBy, cancellationToken);
        return Ok(ApiResponse.Ok(result, "违规记录登记成功"));
    }

    /// <summary>VIOL-02 查询违规记录。</summary>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<ViolationDto>>>> GetPaged(
        [FromQuery] ViolationQueryDto query,
        CancellationToken cancellationToken)
    {
        var result = await _service.GetPagedAsync(query, cancellationToken);
        return Ok(ApiResponse.Ok(result));
    }

    /// <summary>VIOL-03 删除违规记录（仅超管）。</summary>
    [Authorize(Roles = AuthPolicies.SuperAdmin)]
    [HttpDelete("{violationsId}")]
    public async Task<IActionResult> DeleteViolation(int violationsId, [FromBody] DeleteViolationRequest request)
    {
        // 1. 查找违规记录
        var violation = await _context.ViolationRecords.FindAsync(violationsId);
        if (violation == null)
            return NotFound(ApiResponse.Error(404, "违规记录不存在"));

        // 2. 校验删除原因
        if (string.IsNullOrWhiteSpace(request.Reason))
            return BadRequest(ApiResponse.Error(400, "请注明删除原因"));

        // 3. 记录详细信息（用于审计）
        var details = $"删除违规记录 ID {violationsId}，学生：{violation.StudentId}，类型：{violation.VioType}，日期：{violation.VioDate:yyyy-MM-dd}，原因：{request.Reason}";

        // 4. 执行删除
        _context.ViolationRecords.Remove(violation);
        await _context.SaveChangesAsync();

        // 5. 写入审计日志
        await _auditService.LogEventAsync(
            eventType: $"DELETE /api/violations/{violationsId}",
            targetType: "Violation",
            targetId: violationsId.ToString(),
            actorAccountId: GetCurrentUserId(),
            details: details
        );

        return Ok(ApiResponse.Ok(new { message = $"违规记录 ID {violationsId} 已删除" }));
    }

    // ===== DTO 定义 =====
    public class DeleteViolationRequest
    {
        public string Reason { get; set; } = string.Empty;
    }

    // ===== 辅助方法 =====
    private int? GetCurrentUserId()
    {
        var claim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        return int.TryParse(claim, out var id) ? id : null;
    }
}