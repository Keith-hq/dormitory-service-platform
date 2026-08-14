using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TemplateDormApi.DTO;
using TemplateDormApi.Security;
using TemplateDormApi.Services;

namespace TemplateDormApi.Controllers;

[ApiController]
[Authorize(Roles = AuthPolicies.SuperAdmin)]
[Route("students")]
public class StudentsController : ControllerBase
{
    private readonly IAuditService _auditService;
    private readonly ILogger<StudentsController> _logger;

    public StudentsController(IAuditService auditService, ILogger<StudentsController> logger)
    {
        _auditService = auditService;
        _logger = logger;
    }

    // GET /students - 获取所有学生档案（SUPER-03）
    [HttpGet]
    public async Task<IActionResult> GetStudents()
    {
        await _auditService.LogEventAsync(
            eventType: "GET /students",
            targetType: "Student",
            actorAccountId: GetCurrentUserId()
        );

        return StatusCode(501, ApiResponse.Error(501, "学生档案列表接口尚未实现"));
    }

    // POST /students - 新增学生档案（SUPER-03）
    [HttpPost]
    public async Task<IActionResult> CreateStudent([FromBody] CreateStudentRequest request)
    {
        await _auditService.LogEventAsync(
            eventType: "POST /students",
            targetType: "Student",
            actorAccountId: GetCurrentUserId()
        );

        return StatusCode(501, ApiResponse.Error(501, "创建学生档案接口尚未实现"));
    }

    // PUT /students/{studentId} - 修改学生档案（SUPER-03）
    [HttpPut("{studentId}")]
    public async Task<IActionResult> UpdateStudent(string studentId, [FromBody] UpdateStudentRequest request)
    {
        await _auditService.LogEventAsync(
            eventType: $"PUT /students/{studentId}",
            targetType: "Student",
            targetId: studentId,
            actorAccountId: GetCurrentUserId()
        );

        return StatusCode(501, ApiResponse.Error(501, "修改学生档案接口尚未实现"));
    }

    // DELETE /students/{studentId} - 删除学生档案（契约未明确，但作为资源化路径保留）
    [HttpDelete("{studentId}")]
    public async Task<IActionResult> DeleteStudent(string studentId)
    {
        await _auditService.LogEventAsync(
            eventType: $"DELETE /students/{studentId}",
            targetType: "Student",
            targetId: studentId,
            actorAccountId: GetCurrentUserId()
        );

        return StatusCode(501, ApiResponse.Error(501, "删除学生档案接口尚未实现"));
    }

    // POST /students/import - Excel 批量导入学生（IMPORT-01，独立接口）
    [HttpPost("import")]
    public async Task<IActionResult> ImportStudents([FromBody] ImportStudentsRequest request)
    {
        await _auditService.LogEventAsync(
            eventType: "POST /students/import",
            targetType: "Student",
            actorAccountId: GetCurrentUserId()
        );

        return StatusCode(501, ApiResponse.Error(501, "学生批量导入接口尚未实现"));
    }

    // ===== DTO 定义 =====
    public class CreateStudentRequest
    {
        public string StudentId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Gender { get; set; }
        public int? MajorId { get; set; }
        public string? Phone { get; set; }
        public string? Email { get; set; }
    }

    public class UpdateStudentRequest
    {
        public string Name { get; set; } = string.Empty;
        public string? Gender { get; set; }
        public int? MajorId { get; set; }
        public string? Phone { get; set; }
        public string? Email { get; set; }
    }

    public class ImportStudentsRequest
    {
        public List<CreateStudentRequest> Students { get; set; } = new();
    }

    // ===== 辅助方法 =====
    private int? GetCurrentUserId()
    {
        var claim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        return int.TryParse(claim, out var id) ? id : null;
    }
}