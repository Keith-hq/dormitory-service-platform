using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TemplateDormApi.DTO;
using TemplateDormApi.Security;
using TemplateDormApi.Services;

namespace TemplateDormApi.Controllers;

[ApiController]
[Authorize(Roles = AuthPolicies.SuperAdmin)]
[Route("audit-events")]
public class AuditEventsController : ControllerBase
{
    private readonly IAuditService _auditService;
    private readonly ILogger<AuditEventsController> _logger;

    public AuditEventsController(IAuditService auditService, ILogger<AuditEventsController> logger)
    {
        _auditService = auditService;
        _logger = logger;
    }

    // GET /audit-events - 审计日志查询（SUPER-07）
    [HttpGet]
    public async Task<IActionResult> GetAuditEvents([FromQuery] AuditEventQueryDto query)
    {
        await _auditService.LogEventAsync(
            eventType: "GET /audit-events",
            targetType: "AuditEvent",
            actorAccountId: GetCurrentUserId()
        );

        return StatusCode(501, ApiResponse.Error(501, "审计日志查询接口尚未实现"));
    }

    // ===== DTO 定义 =====
    public class AuditEventQueryDto
    {
        public string? EventType { get; set; }
        public string? TargetType { get; set; }
        public string? TargetId { get; set; }
        public int? ActorAccountId { get; set; }
        public DateTime? StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public int? Page { get; set; } = 1;
        public int? PageSize { get; set; } = 20;
    }

    // ===== 辅助方法 =====
    private int? GetCurrentUserId()
    {
        var claim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        return int.TryParse(claim, out var id) ? id : null;
    }
}