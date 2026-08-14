using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TemplateDormApi.Data;
using TemplateDormApi.DTO;
using TemplateDormApi.Models;
using TemplateDormApi.Security;
using TemplateDormApi.Services;

namespace TemplateDormApi.Controllers;

[ApiController]
[Authorize(Roles = AuthPolicies.SuperAdmin)]
[Route("majors")]
public class MajorsController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IAuditService _auditService;
    private readonly ILogger<MajorsController> _logger;

    public MajorsController(
        AppDbContext context,
        IAuditService auditService,
        ILogger<MajorsController> logger)
    {
        _context = context;
        _auditService = auditService;
        _logger = logger;
    }

    // GET /majors - 获取所有专业（关联学院名称）
    [HttpGet]
    public async Task<IActionResult> GetMajors()
    {
        var majors = await _context.Majors
            .Include(m => m.College)
            .OrderBy(m => m.MajorId)
            .Select(m => new
            {
                m.MajorId,
                m.MajorName,
                CollegeId = m.CollegeId,
                CollegeName = m.College != null ? m.College.CollegeName : null
            })
            .ToListAsync();

        await _auditService.LogEventAsync(
            eventType: "GET /majors",
            targetType: "Major",
            actorAccountId: GetCurrentUserId()
        );

        return Ok(ApiResponse.Ok(majors));
    }

    // POST /majors - 新增专业
    [HttpPost]
    public async Task<IActionResult> CreateMajor([FromBody] CreateMajorRequest request)
    {
        // 参数校验
        if (string.IsNullOrWhiteSpace(request.MajorName))
            return BadRequest(ApiResponse.Error(400, "专业名称不能为空"));

        if (!request.CollegeId.HasValue || request.CollegeId.Value <= 0)
            return BadRequest(ApiResponse.Error(400, "请指定所属学院"));

        // 检查学院是否存在
        var collegeExists = await _context.Colleges
            .CountAsync(c => c.CollegeId == request.CollegeId.Value) > 0;
        if (!collegeExists)
            return BadRequest(ApiResponse.Error(400, "指定的学院不存在"));

        // 检查专业名称是否已存在（全局唯一）
        var exists = await _context.Majors
            .CountAsync(m => m.MajorName == request.MajorName) > 0;
        if (exists)
            return BadRequest(ApiResponse.Error(400, "专业名称已存在"));

        // 创建实体
        var major = new Major
        {
            MajorName = request.MajorName.Trim(),
            CollegeId = request.CollegeId.Value
        };

        _context.Majors.Add(major);
        await _context.SaveChangesAsync();

        // 审计日志
        await _auditService.LogEventAsync(
            eventType: "POST /majors",
            targetType: "Major",
            targetId: major.MajorId.ToString(),
            actorAccountId: GetCurrentUserId(),
            details: $"创建专业：{major.MajorName}，所属学院ID：{major.CollegeId}"
        );

        return Ok(ApiResponse.Ok(new
        {
            major.MajorId,
            major.MajorName,
            major.CollegeId
        }));
    }

    // PUT /majors/{id} - 修改专业
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateMajor(int id, [FromBody] UpdateMajorRequest request)
    {
        // 查找专业
        var major = await _context.Majors.FindAsync(id);
        if (major == null)
            return NotFound(ApiResponse.Error(404, "专业不存在"));

        // 参数校验
        if (string.IsNullOrWhiteSpace(request.MajorName))
            return BadRequest(ApiResponse.Error(400, "专业名称不能为空"));

        if (!request.CollegeId.HasValue || request.CollegeId.Value <= 0)
            return BadRequest(ApiResponse.Error(400, "请指定所属学院"));

        // 检查学院是否存在
        var collegeExists = await _context.Colleges
            .CountAsync(c => c.CollegeId == request.CollegeId.Value) > 0;
        if (!collegeExists)
            return BadRequest(ApiResponse.Error(400, "指定的学院不存在"));

        // 检查专业名称是否已存在（排除自身）
        var exists = await _context.Majors
            .CountAsync(m => m.MajorName == request.MajorName && m.MajorId != id) > 0;
        if (exists)
            return BadRequest(ApiResponse.Error(400, "专业名称已存在"));

        // 更新字段
        major.MajorName = request.MajorName.Trim();
        major.CollegeId = request.CollegeId.Value;

        await _context.SaveChangesAsync();

        // 审计日志
        await _auditService.LogEventAsync(
            eventType: $"PUT /majors/{id}",
            targetType: "Major",
            targetId: id.ToString(),
            actorAccountId: GetCurrentUserId(),
            details: $"修改专业 ID {id}：{major.MajorName}，所属学院ID：{major.CollegeId}"
        );

        return Ok(ApiResponse.Ok(new
        {
            major.MajorId,
            major.MajorName,
            major.CollegeId
        }));
    }

    // DELETE /majors/{id} - 删除专业
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteMajor(int id)
    {
        // 查找专业
        var major = await _context.Majors.FindAsync(id);
        if (major == null)
            return NotFound(ApiResponse.Error(404, "专业不存在"));

        // 检查是否有学生引用
        var hasStudents = await _context.Students
            .CountAsync(s => s.MajorId == id) > 0;
        if (hasStudents)
            return BadRequest(ApiResponse.Error(400, "该专业下存在学生，无法删除，请先转移或删除学生"));

        // 删除
        _context.Majors.Remove(major);
        await _context.SaveChangesAsync();

        // 审计日志
        await _auditService.LogEventAsync(
            eventType: $"DELETE /majors/{id}",
            targetType: "Major",
            targetId: id.ToString(),
            actorAccountId: GetCurrentUserId(),
            details: $"删除专业 ID {id}：{major.MajorName}"
        );

        return Ok(ApiResponse.Ok(new { message = $"专业 {major.MajorName} 删除成功" }));
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