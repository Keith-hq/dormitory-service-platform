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
[Route("colleges")]
public class CollegesController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IAuditService _auditService;
    private readonly ILogger<CollegesController> _logger;

    public CollegesController(
        AppDbContext context,
        IAuditService auditService,
        ILogger<CollegesController> logger)
    {
        _context = context;
        _auditService = auditService;
        _logger = logger;
    }

    // GET /colleges - 获取所有学院
    [HttpGet]
    public async Task<IActionResult> GetColleges()
    {
        var colleges = await _context.Colleges
            .OrderBy(c => c.CollegeId)
            .Select(c => new
            {
                c.CollegeId,
                c.CollegeName,
                c.CounselorName,
                c.ContactPhone
            })
            .ToListAsync();

        await _auditService.LogEventAsync(
            eventType: "GET /colleges",
            targetType: "College",
            actorAccountId: GetCurrentUserId()
        );

        return Ok(ApiResponse.Ok(colleges));
    }

    // POST /colleges - 新增学院
    [HttpPost]
    public async Task<IActionResult> CreateCollege([FromBody] CreateCollegeRequest request)
    {
        // 参数校验
        if (string.IsNullOrWhiteSpace(request.CollegeName))
            return BadRequest(ApiResponse.Error(400, "学院名称不能为空"));

        // 检查重名
        var exists = await _context.Colleges
            .CountAsync(c => c.CollegeName == request.CollegeName) > 0;
        if (exists)
            return BadRequest(ApiResponse.Error(400, "学院名称已存在"));

        // 创建实体
        var college = new College
        {
            CollegeName = request.CollegeName.Trim(),
            CounselorName = request.CounselorName?.Trim(),
            ContactPhone = request.ContactPhone?.Trim()
        };

        _context.Colleges.Add(college);
        await _context.SaveChangesAsync();

        // 审计日志
        await _auditService.LogEventAsync(
            eventType: "POST /colleges",
            targetType: "College",
            targetId: college.CollegeId.ToString(),
            actorAccountId: GetCurrentUserId(),
            details: $"创建学院：{college.CollegeName}"
        );

        return Ok(ApiResponse.Ok(new
        {
            college.CollegeId,
            college.CollegeName,
            college.CounselorName,
            college.ContactPhone
        }));
    }

    // PUT /colleges/{id} - 修改学院
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateCollege(int id, [FromBody] UpdateCollegeRequest request)
    {
        // 查找学院
        var college = await _context.Colleges.FindAsync(id);
        if (college == null)
            return NotFound(ApiResponse.Error(404, "学院不存在"));

        // 参数校验
        if (string.IsNullOrWhiteSpace(request.CollegeName))
            return BadRequest(ApiResponse.Error(400, "学院名称不能为空"));

        // 检查重名（排除自身）
        var exists = await _context.Colleges
            .CountAsync(c => c.CollegeName == request.CollegeName && c.CollegeId != id) > 0;
        if (exists)
            return BadRequest(ApiResponse.Error(400, "学院名称已存在"));

        // 更新字段
        college.CollegeName = request.CollegeName.Trim();
        college.CounselorName = request.CounselorName?.Trim();
        college.ContactPhone = request.ContactPhone?.Trim();

        await _context.SaveChangesAsync();

        // 审计日志
        await _auditService.LogEventAsync(
            eventType: $"PUT /colleges/{id}",
            targetType: "College",
            targetId: id.ToString(),
            actorAccountId: GetCurrentUserId(),
            details: $"修改学院 ID {id}：{college.CollegeName}"
        );

        return Ok(ApiResponse.Ok(new
        {
            college.CollegeId,
            college.CollegeName,
            college.CounselorName,
            college.ContactPhone
        }));
    }

    // DELETE /colleges/{id} - 删除学院
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteCollege(int id)
    {
        // 查找学院
        var college = await _context.Colleges.FindAsync(id);
        if (college == null)
            return NotFound(ApiResponse.Error(404, "学院不存在"));

        // 检查是否有专业引用
        var hasMajors = await _context.Majors
            .CountAsync(m => m.CollegeId == id) > 0;
        if (hasMajors)
            return BadRequest(ApiResponse.Error(400, "该学院下存在专业，无法删除，请先删除或转移专业"));

        // 删除
        _context.Colleges.Remove(college);
        await _context.SaveChangesAsync();

        // 审计日志
        await _auditService.LogEventAsync(
            eventType: $"DELETE /colleges/{id}",
            targetType: "College",
            targetId: id.ToString(),
            actorAccountId: GetCurrentUserId(),
            details: $"删除学院 ID {id}：{college.CollegeName}"
        );

        return Ok(ApiResponse.Ok(new { message = $"学院 {college.CollegeName} 删除成功" }));
    }

    // ===== DTO 定义 =====
    public class CreateCollegeRequest
    {
        public string CollegeName { get; set; } = string.Empty;
        public string? CounselorName { get; set; }
        public string? ContactPhone { get; set; }
    }

    public class UpdateCollegeRequest
    {
        public string CollegeName { get; set; } = string.Empty;
        public string? CounselorName { get; set; }
        public string? ContactPhone { get; set; }
    }

    // ===== 辅助方法 =====
    private int? GetCurrentUserId()
    {
        var claim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        return int.TryParse(claim, out var id) ? id : null;
    }
}