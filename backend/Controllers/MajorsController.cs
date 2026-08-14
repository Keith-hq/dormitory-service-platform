using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TemplateDormApi.DTO;
using TemplateDormApi.Security;
using TemplateDormApi.Services;

namespace TemplateDormApi.Controllers;

[ApiController]
[Authorize(Roles = AuthPolicies.SuperAdmin)]
[Route("majors")]
public class MajorsController : ControllerBase
{
    private readonly IAuditService _auditService;
    private readonly ILogger<MajorsController> _logger;

    public MajorsController(IAuditService auditService, ILogger<MajorsController> logger)
    {
        _auditService = auditService;
        _logger = logger;
    }

    // GET /majors - 获取所有专业
    [HttpGet]
    public async Task<IActionResult> GetMajors()
    {
        // 记录审计日志（可选）
        await _auditService.LogEventAsync(
            eventType: "GET /majors",
            targetType: "Major",
            actorAccountId: GetCurrentUserId()
        );

        // 未实现，返回 501
        return StatusCode(501, ApiResponse.Error(501, "专业列表接口尚未实现"));
    }

    // POST /majors - 新增专业
    [HttpPost]
    public async Task<IActionResult> CreateMajor([FromBody] CreateMajorRequest request)
    {
        await _auditService.LogEventAsync(
            eventType: "POST /majors",
            targetType: "Major",
            actorAccountId: GetCurrentUserId()
        );

        return StatusCode(501, ApiResponse.Error(501, "创建专业接口尚未实现"));
    }

    // PUT /majors/{id} - 修改专业
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateMajor(int id, [FromBody] UpdateMajorRequest request)
    {
        await _auditService.LogEventAsync(
            eventType: $"PUT /majors/{id}",
            targetType: "Major",
            targetId: id.ToString(),
            actorAccountId: GetCurrentUserId()
        );

        return StatusCode(501, ApiResponse.Error(501, "修改专业接口尚未实现"));
    }

    // DELETE /majors/{id} - 删除专业
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteMajor(int id)
    {
        await _auditService.LogEventAsync(
            eventType: $"DELETE /majors/{id}",
            targetType: "Major",
            targetId: id.ToString(),
            actorAccountId: GetCurrentUserId()
        );

        return StatusCode(501, ApiResponse.Error(501, "删除专业接口尚未实现"));
    }

    // ===== DTO 定义 =====
    public class CreateMajorRequest
    {
        public string MajorName { get; set; } = string.Empty;
        public int? CollegeId { get; set; }
    }

    public class UpdateMajorRequest
    {
        public string MajorName { get; set; } = string.Empty;
        public int? CollegeId { get; set; }
    }

    // ===== 辅助方法 =====
    private int? GetCurrentUserId()
    {
        var claim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        return int.TryParse(claim, out var id) ? id : null;
    }
}