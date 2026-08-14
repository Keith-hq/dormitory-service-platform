using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TemplateDormApi.DTO;
using TemplateDormApi.Security;
using TemplateDormApi.Services;

namespace TemplateDormApi.Controllers;

[ApiController]
[Authorize(Roles = AuthPolicies.SuperAdmin)]
[Route("admins")]
public class AdminsController : ControllerBase
{
    private readonly IAuditService _auditService;
    private readonly ILogger<AdminsController> _logger;

    public AdminsController(IAuditService auditService, ILogger<AdminsController> logger)
    {
        _auditService = auditService;
        _logger = logger;
    }

    // GET /admins - 宿管列表（SUPER-04）
    [HttpGet]
    public async Task<IActionResult> GetAdmins()
    {
        await _auditService.LogEventAsync(
            eventType: "GET /admins",
            targetType: "Admin",
            actorAccountId: GetCurrentUserId()
        );

        return StatusCode(501, ApiResponse.Error(501, "宿管列表接口尚未实现"));
    }

    // PUT /admins/{id} - 编辑宿管信息（SUPER-05）
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateAdmin(string id, [FromBody] UpdateAdminRequest request)
    {
        await _auditService.LogEventAsync(
            eventType: $"PUT /admins/{id}",
            targetType: "Admin",
            targetId: id,
            actorAccountId: GetCurrentUserId()
        );

        return StatusCode(501, ApiResponse.Error(501, "编辑宿管接口尚未实现"));
    }

    // DELETE /admins/{id}/disable - 停用宿管（SUPER-06）
    // 契约要求：楼长先移交在办事项，停用后 5 分钟内会话失效
    [HttpDelete("{id}/disable")]
    public async Task<IActionResult> DisableAdmin(string id, [FromBody] DisableAdminRequest request)
    {
        await _auditService.LogEventAsync(
            eventType: $"DELETE /admins/{id}/disable",
            targetType: "Admin",
            targetId: id,
            actorAccountId: GetCurrentUserId()
        );

        return StatusCode(501, ApiResponse.Error(501, "停用宿管接口尚未实现"));
    }

    // POST /admins/{id}/password - 重置宿管密码（PWD-02）
    // 契约要求：生成 8 位随机初始密码，明文返回，并写入审计日志
    [HttpPost("{id}/password")]
    public async Task<IActionResult> ResetPassword(string id)
    {
        // 生成 8 位随机密码（暂未实现，返回占位）
        string newPassword = "Temp@123"; // TODO: 实际应随机生成

        await _auditService.LogEventAsync(
            eventType: $"POST /admins/{id}/password",
            targetType: "Admin",
            targetId: id,
            actorAccountId: GetCurrentUserId()
        );

        return StatusCode(501, ApiResponse.Error(501, "重置密码接口尚未实现"));
    }

    // ===== DTO 定义 =====
    public class UpdateAdminRequest
    {
        public string AdminName { get; set; } = string.Empty;
        public string? Phone { get; set; }
        public string? RoleLevel { get; set; }
        public int? BuildingId { get; set; }
    }

    public class DisableAdminRequest
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