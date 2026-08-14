using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TemplateDormApi.DTO;
using TemplateDormApi.Security;
using TemplateDormApi.Services;

namespace TemplateDormApi.Controllers;

[ApiController]
[Authorize(Roles = AuthPolicies.SuperAdmin)]
[Route("reports")]
public class ReportsController : ControllerBase
{
    private readonly IAuditService _auditService;
    private readonly ILogger<ReportsController> _logger;

    public ReportsController(IAuditService auditService, ILogger<ReportsController> logger)
    {
        _auditService = auditService;
        _logger = logger;
    }

    // GET /reports/{type} - 统计报表（REPT-01）
    // type: 7 类统计报表，例如：occupancy, utility, repair, hygiene, credit, visitor, violation
    [HttpGet("{type}")]
    public async Task<IActionResult> GetReport(string type, [FromQuery] ReportQueryDto query)
    {
        await _auditService.LogEventAsync(
            eventType: $"GET /reports/{type}",
            targetType: "Report",
            targetId: type,
            actorAccountId: GetCurrentUserId()
        );

        return StatusCode(501, ApiResponse.Error(501, $"报表 {type} 接口尚未实现"));
    }

    // ===== DTO 定义 =====
    public class ReportQueryDto
    {
        public string? StartDate { get; set; }
        public string? EndDate { get; set; }
        public string? BuildingId { get; set; }
        public string? RoomId { get; set; }
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