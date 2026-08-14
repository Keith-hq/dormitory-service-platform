using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TemplateDormApi.DTO;
using TemplateDormApi.Security;
using TemplateDormApi.Services;

namespace TemplateDormApi.Controllers;

[ApiController]
[Authorize(Roles = AuthPolicies.SuperAdmin)]
[Route("violations")]
public class ViolationsController : ControllerBase
{
    private readonly IAuditService _auditService;
    private readonly ILogger<ViolationsController> _logger;

    public ViolationsController(IAuditService auditService, ILogger<ViolationsController> logger)
    {
        _auditService = auditService;
        _logger = logger;
    }

    // DELETE /violations/{id} - 删除违规记录（VIOL-03）
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteViolation(int id, [FromBody] DeleteViolationRequest request)
    {
        await _auditService.LogEventAsync(
            eventType: $"DELETE /violations/{id}",
            targetType: "Violation",
            targetId: id.ToString(),
            actorAccountId: GetCurrentUserId()
        );

        return StatusCode(501, ApiResponse.Error(501, "删除违规记录接口尚未实现"));
    }

    // ===== DTO 定义 =====
    public class DeleteViolationRequest
    {
        public string Reason { get; set; } = string.Empty; // 删除原因，必填
    }

    // ===== 辅助方法 =====
    private int? GetCurrentUserId()
    {
        var claim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        return int.TryParse(claim, out var id) ? id : null;
    }
}