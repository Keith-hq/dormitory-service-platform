using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TemplateDormApi.Data;
using TemplateDormApi.DTO;
using TemplateDormApi.Models;
using TemplateDormApi.Security;
using TemplateDormApi.Services;
using OfficeOpenXml;

namespace TemplateDormApi.Controllers;

[ApiController]
[Authorize(Roles = AuthPolicies.SuperAdmin)]
[Route("api/students")]
public class StudentsController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IAuditService _auditService;
    private readonly ILogger<StudentsController> _logger;
    private readonly IImportService _importService;

    public StudentsController(
        AppDbContext context,
        IAuditService auditService,
        ILogger<StudentsController> logger,
        IImportService importService)
    {
        _context = context;
        _auditService = auditService;
        _logger = logger;
        _importService = importService;
    }

    // GET /students - 获取所有学生档案（SUPER-03）
    [HttpGet]
    public async Task<IActionResult> GetStudents()
    {
        var students = await _context.Students
            .Include(s => s.Major)
                .ThenInclude(m => m!.College)
            .OrderBy(s => s.StudentId)
            .Select(s => new
            {
                s.StudentId,
                s.Name,
                s.Gender,
                s.Phone,
                s.Email,
                MajorId = s.MajorId,
                MajorName = s.Major != null ? s.Major.MajorName : null,
                CollegeName = s.Major != null && s.Major.College != null ? s.Major.College.CollegeName : null
            })
            .ToListAsync();

        await _auditService.LogEventAsync(
            eventType: "GET /students",
            targetType: "Student",
            actorAccountId: GetCurrentUserId()
        );

        return Ok(ApiResponse.Ok(students));
    }

    // POST /students - 新增学生档案（SUPER-03）
    [HttpPost]
    public async Task<IActionResult> CreateStudent([FromBody] CreateStudentRequest request)
    {
        // 参数校验
        if (string.IsNullOrWhiteSpace(request.StudentId))
            return BadRequest(ApiResponse.Error(400, "学号不能为空"));
        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest(ApiResponse.Error(400, "姓名不能为空"));

        // 检查学号是否已存在
        var exists = await _context.Students
            .CountAsync(s => s.StudentId == request.StudentId) > 0;
        if (exists)
            return BadRequest(ApiResponse.Error(400, "学号已存在"));

        // 检查专业是否存在（如果指定）
        if (request.MajorId.HasValue)
        {
            var majorExists = await _context.Majors
                .CountAsync(m => m.MajorId == request.MajorId.Value) > 0;
            if (!majorExists)
                return BadRequest(ApiResponse.Error(400, "指定的专业不存在"));
        }

        // 创建学生实体
        var student = new Student
        {
            StudentId = request.StudentId.Trim(),
            Name = request.Name.Trim(),
            Gender = request.Gender?.Trim(),
            MajorId = request.MajorId,
            Phone = request.Phone?.Trim(),
            Email = request.Email?.Trim()
        };

        _context.Students.Add(student);
        await _context.SaveChangesAsync();

        // 审计日志
        await _auditService.LogEventAsync(
            eventType: "POST /students",
            targetType: "Student",
            targetId: student.StudentId,
            actorAccountId: GetCurrentUserId(),
            details: $"创建学生：{student.StudentId} - {student.Name}"
        );

        return Ok(ApiResponse.Ok(new
        {
            student.StudentId,
            student.Name,
            student.Gender,
            student.MajorId,
            student.Phone,
            student.Email
        }));
    }

    // PUT /students/{studentId} - 修改学生档案（SUPER-03）
    [HttpPut("{studentId}")]
    public async Task<IActionResult> UpdateStudent(string studentId, [FromBody] UpdateStudentRequest request)
    {
        // 查找学生
        var student = await _context.Students.FindAsync(studentId);
        if (student == null)
            return NotFound(ApiResponse.Error(404, "学生不存在"));

        // 参数校验
        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest(ApiResponse.Error(400, "姓名不能为空"));

        // 检查专业是否存在（如果指定）
        if (request.MajorId.HasValue)
        {
            var majorExists = await _context.Majors
                .CountAsync(m => m.MajorId == request.MajorId.Value) > 0;
            if (!majorExists)
                return BadRequest(ApiResponse.Error(400, "指定的专业不存在"));
        }

        // 更新字段
        student.Name = request.Name.Trim();
        student.Gender = request.Gender?.Trim();
        student.MajorId = request.MajorId;
        student.Phone = request.Phone?.Trim();
        student.Email = request.Email?.Trim();

        await _context.SaveChangesAsync();

        // 审计日志
        await _auditService.LogEventAsync(
            eventType: $"PUT /students/{studentId}",
            targetType: "Student",
            targetId: studentId,
            actorAccountId: GetCurrentUserId(),
            details: $"修改学生：{studentId} - {student.Name}"
        );

        return Ok(ApiResponse.Ok(new
        {
            student.StudentId,
            student.Name,
            student.Gender,
            student.MajorId,
            student.Phone,
            student.Email
        }));
    }

    // DELETE /students/{studentId} - 删除学生档案
    [HttpDelete("{studentId}")]
    public async Task<IActionResult> DeleteStudent(string studentId)
    {
        // 查找学生
        var student = await _context.Students.FindAsync(studentId);
        if (student == null)
            return NotFound(ApiResponse.Error(404, "学生不存在"));

        // 检查是否有床位分配
        var hasBedAllocation = await _context.BedAllocations
            .CountAsync(b => b.StudentId == studentId && b.CheckOutDate == null) > 0;
        if (hasBedAllocation)
            return BadRequest(ApiResponse.Error(400, "该学生当前有在住床位，无法删除，请先办理退宿"));

        // 检查是否有未完成的报修
        var hasPendingRepair = await _context.RepairTickets
            .CountAsync(r => r.StudentId == studentId && r.Status != "已完成" && r.Status != "已取消") > 0;
        if (hasPendingRepair)
            return BadRequest(ApiResponse.Error(400, "该学生有未完成的报修，无法删除，请先处理报修"));

        // 检查是否有未归还的共享物品
        var hasUnreturnedItem = await _context.ItemLoans
            .CountAsync(i => i.StudentId == studentId && i.ReturnTime == null) > 0;
        if (hasUnreturnedItem)
            return BadRequest(ApiResponse.Error(400, "该学生有未归还的共享物品，无法删除，请先归还物品"));

        // 删除
        _context.Students.Remove(student);
        await _context.SaveChangesAsync();

        // 审计日志
        await _auditService.LogEventAsync(
            eventType: $"DELETE /students/{studentId}",
            targetType: "Student",
            targetId: studentId,
            actorAccountId: GetCurrentUserId(),
            details: $"删除学生：{studentId} - {student.Name}"
        );

        return Ok(ApiResponse.Ok(new { message = $"学生 {student.Name} 删除成功" }));
    }

    // POST /students/import - Excel 批量导入学生（IMPORT-01）
    [HttpPost("import")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> ImportStudents([FromForm] IFormFile file)
    {
        var importResult = await _importService.ImportStudentsAsync(file, CancellationToken.None);
        var importedCount = importResult.ImportedCount;
        var errors = importResult.Errors;
        var skippedRows = importResult.SkippedRows;

        // 处理错误
        if (errors.Count > 10)
        {
            return BadRequest(ApiResponse.Error(400, $"导入失败，共 {errors.Count} 条错误，超过 10 条限制", errors));
        }
        if (errors.Count > 0)
        {
            return BadRequest(ApiResponse.Error(400, $"存在 {errors.Count} 条错误，请修正后重新上传", errors));
        }

        // 写入审计日志
        await _auditService.LogEventAsync(
            eventType: "POST /students/import",
            targetType: "Student",
            actorAccountId: GetCurrentUserId(),
            details: $"批量导入学生 {importedCount} 条，跳过 {skippedRows.Count} 行（已存在）"
        );

        return Ok(ApiResponse.Ok(new
        {
            importedCount,
            skippedRows,
            message = "导入成功"
        }));
    }

    // 辅助类
    public class ImportError
    {
        public int Row { get; set; }
        public List<string> Messages { get; set; } = new();
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