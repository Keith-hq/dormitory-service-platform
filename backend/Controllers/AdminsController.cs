using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TemplateDormApi.Data;
using TemplateDormApi.DTO;
using TemplateDormApi.Security;
using TemplateDormApi.Services;

namespace TemplateDormApi.Controllers;

[ApiController]
[Authorize(Roles = AuthPolicies.SuperAdmin)]
[Route("admins")]
public class AdminsController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IAuditService _auditService;
    private readonly ILogger<AdminsController> _logger;

    public AdminsController(
        AppDbContext context,
        IAuditService auditService,
        ILogger<AdminsController> logger)
    {
        _context = context;
        _auditService = auditService;
        _logger = logger;
    }

    // GET /admins - 宿管列表（SUPER-04）
    [HttpGet]
    public async Task<IActionResult> GetAdmins()
    {
        var admins = await _context.Admins
            .Include(a => a.Building)
            .OrderBy(a => a.AdminId)
            .Select(a => new
            {
                a.AdminId,
                a.AdminName,
                a.Phone,
                a.RoleLevel,
                BuildingId = a.BuildingId,
                BuildingName = a.Building != null ? a.Building.BuildingName : null
            })
            .ToListAsync();

        await _auditService.LogEventAsync(
            eventType: "GET /admins",
            targetType: "Admin",
            actorAccountId: GetCurrentUserId()
        );

        return Ok(ApiResponse.Ok(admins));
    }

    // PUT /admins/{id} - 编辑宿管信息（SUPER-05）
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateAdmin(string id, [FromBody] UpdateAdminRequest request)
    {
        // 查找宿管
        var admin = await _context.Admins.FindAsync(id);
        if (admin == null)
            return NotFound(ApiResponse.Error(404, "宿管不存在"));

        // 参数校验
        if (string.IsNullOrWhiteSpace(request.AdminName))
            return BadRequest(ApiResponse.Error(400, "姓名不能为空"));

        // 检查角色是否合法（中文值）
        var validRoles = new[] { "超级管理员", "楼长", "维修员" };
        if (!validRoles.Contains(request.RoleLevel))
            return BadRequest(ApiResponse.Error(400, "角色必须是：超级管理员、楼长、维修员"));

        // 如果指定了楼栋，检查是否存在
        if (request.BuildingId.HasValue)
        {
            var buildingExists = await _context.Buildings
                .CountAsync(b => b.BuildingId == request.BuildingId.Value) > 0;
            if (!buildingExists)
                return BadRequest(ApiResponse.Error(400, "指定的楼栋不存在"));
        }

        // 更新字段
        admin.AdminName = request.AdminName.Trim();
        admin.Phone = request.Phone?.Trim();
        admin.RoleLevel = request.RoleLevel;
        admin.BuildingId = request.BuildingId;

        await _context.SaveChangesAsync();

        // 审计日志
        await _auditService.LogEventAsync(
            eventType: $"PUT /admins/{id}",
            targetType: "Admin",
            targetId: id,
            actorAccountId: GetCurrentUserId(),
            details: $"修改宿管 {id}：{admin.AdminName}，角色：{admin.RoleLevel}"
        );

        return Ok(ApiResponse.Ok(new
        {
            admin.AdminId,
            admin.AdminName,
            admin.Phone,
            admin.RoleLevel,
            admin.BuildingId
        }));
    }

    // DELETE /admins/{id}/disable - 停用宿管（SUPER-06）
    // 契约要求：楼长须先移交在办事项；停用后 5 分钟内会话失效
    [HttpDelete("{id}/disable")]
    public async Task<IActionResult> DisableAdmin(string id, [FromBody] DisableAdminRequest request)
    {
        // 1. 检查宿管是否存在
        var admin = await _context.Admins.FindAsync(id);
        if (admin == null)
            return NotFound(ApiResponse.Error(404, "宿管不存在"));

        // 2. 如果是楼长，检查是否有在办事项（已分配未完成的工单）
        if (admin.RoleLevel == "楼长")
        {
            var hasPending = await _context.RepairTickets
                .CountAsync(t => t.AssignedTo == id && t.Status != "已完成" && t.Status != "已取消") > 0;
            if (hasPending)
            {
                // 返回提示需要先移交（契约要求）
                return BadRequest(ApiResponse.Error(400, "该楼长有未完成工单，请先移交在办事项后再停用"));
            }
        }

        // 3. 停用宿管（软删除或标记状态）
        // 由于 D_Admin 表没有 STATUS 字段，这里我们删除关联的 UserAccount 来实现“停用”
        // 但更稳妥的是先检查是否有 UserAccount，有则设为停用状态
        var userAccount = await _context.UserAccounts
            .FirstOrDefaultAsync(u => u.AdminId == id);
        if (userAccount != null)
        {
            userAccount.AccountStatus = "INACTIVE";
            await _context.SaveChangesAsync();
        }

        // 也可以考虑直接删除 Admin 记录（但会导致关联数据问题），这里不删除

        // 4. 写入审计日志
        await _auditService.LogEventAsync(
            eventType: $"DELETE /admins/{id}/disable",
            targetType: "Admin",
            targetId: id,
            actorAccountId: GetCurrentUserId(),
            details: $"停用宿管 {id}，原因：{request.Reason}"
        );

        return Ok(ApiResponse.Ok(new { message = $"宿管 {admin.AdminName} 已停用，5 分钟内会话将失效" }));
    }

    // POST /admins/{id}/password - 重置宿管密码（PWD-02）
    // 契约要求：下发 8 位随机初始密码，写入审计日志
    [HttpPost("{id}/password")]
    public async Task<IActionResult> ResetPassword(string id)
    {
        // 1. 检查宿管是否存在
        var admin = await _context.Admins.FindAsync(id);
        if (admin == null)
            return NotFound(ApiResponse.Error(404, "宿管不存在"));

        // 2. 查找对应的 UserAccount
        var userAccount = await _context.UserAccounts
            .FirstOrDefaultAsync(u => u.AdminId == id);
        if (userAccount == null)
            return BadRequest(ApiResponse.Error(400, "该宿管没有关联账号，请先创建账号"));

        // 3. 生成 8 位随机初始密码（字母+数字）
        string newPassword = GenerateRandomPassword(8);

        // 4. 更新密码哈希
        userAccount.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
        await _context.SaveChangesAsync();

        // 5. 写入审计日志
        await _auditService.LogEventAsync(
            eventType: $"POST /admins/{id}/password",
            targetType: "Admin",
            targetId: id,
            actorAccountId: GetCurrentUserId(),
            details: $"重置宿管 {id} 的密码"
        );

        // 6. 返回明文密码（仅此一次）
        return Ok(ApiResponse.Ok(new
        {
            message = "密码重置成功",
            newPassword = newPassword
        }));
    }

    // ===== DTO 定义 =====
    public class UpdateAdminRequest
    {
        public string AdminName { get; set; } = string.Empty;
        public string? Phone { get; set; }
        public string RoleLevel { get; set; } = string.Empty; // 中文值：超级管理员、楼长、维修员
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

    private string GenerateRandomPassword(int length)
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghjkmnpqrstuvwxyz23456789";
        var random = new Random();
        return new string(Enumerable.Repeat(chars, length)
            .Select(s => s[random.Next(s.Length)]).ToArray());
    }
}