using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.IdentityModel.JsonWebTokens;
using OfficeOpenXml.FormulaParsing.LexicalAnalysis;
using System.Linq;
using System.Security.Cryptography;
using TemplateDormApi.Data;
using TemplateDormApi.DTO;
using TemplateDormApi.Models;
using TemplateDormApi.Security;
using TemplateDormApi.Services;

namespace TemplateDormApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IJwtService _jwtService;
    private readonly IMemoryCache _cache;
    private readonly ILogger<AuthController> _logger;
    private readonly IAuditService _auditService;
    private readonly IUserAccountService _userAccountService;

    public AuthController(AppDbContext context, IJwtService jwtService, IMemoryCache cache, ILogger<AuthController> logger, IAuditService auditService, IUserAccountService userAccountService)
    {
        _context = context;
        _jwtService = jwtService;
        _cache = cache;
        _logger = logger;
        _auditService = auditService;
        _userAccountService = userAccountService;
    }

    /// <summary>
    /// 登录请求 DTO
    /// </summary>
    public class LoginRequest
    {
        public string LoginName { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }

    /// <summary>
    /// 创建账号请求 DTO
    /// </summary>
    public class CreateStudentAccountRequest
    {
        public string StudentId { get; set; } = string.Empty;
        public string? LoginName { get; set; }
        public string? Password { get; set; }
    }

    public class CreateAdminAccountRequest
    {
        public string AdminId { get; set; } = string.Empty; // required
        public string Name { get; set; } = string.Empty;    // required
        public string Role { get; set; } = string.Empty;    // required
        public int? BuildingId { get; set; }                // optional for cross-building roles
        public string? Post { get; set; }                   // optional
        public string? Password { get; set; }               // optional
    }


    /// <summary>
    /// 修改密码请求 DTO
    /// </summary>
    public class ChangePasswordRequest
    {
        public string OldPassword { get; set; } = string.Empty;
        public string NewPassword { get; set; } = string.Empty;
    }

    /// <summary>
    /// 用户登录接口
    /// </summary>
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        // 1. 查找用户
        var user = await _context.UserAccounts
            .FirstOrDefaultAsync(u => u.LoginName == request.LoginName);

        if (user == null)
            return Unauthorized(ApiResponse.Error(401, "用户名或密码错误"));

        // 2. 检查账号状态
        if (user.AccountStatus != "正常")
            return Unauthorized(ApiResponse.Error(401, "账号已停用，请联系管理员"));

        // 3. 密码验证
        if (!BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            return Unauthorized(ApiResponse.Error(401, "用户名或密码错误"));

        // 4. 角色映射（按 C-038 裁决）
        string? role;
        if (!string.IsNullOrEmpty(user.StudentId))
        {
            role = "student";
        }
        else if (!string.IsNullOrEmpty(user.AdminId))
        {
            var admin = await _context.Admins.FindAsync(user.AdminId);
            if (admin == null)
                return Unauthorized(ApiResponse.Error(401, "管理员信息不存在"));

            role = admin.RoleLevel switch
            {
                "超级管理员" => "super_admin",
                "楼长" => "admin",
                "维修员" => "repairman",
                "辅导员" => "counselor",
                _ => null
            };

            if (string.IsNullOrEmpty(role))
                return Unauthorized(ApiResponse.Error(401, "角色权限异常，请联系管理员"));
        }
        else
        {
            return Unauthorized(ApiResponse.Error(401, "用户身份异常"));
        }

        // 5. 生成 Token 并检测是否为首次登录
        var token = await _jwtService.GenerateToken(user, role);
        bool needChangePassword = user.IsFirstLogin == "Y";

        // 6. 返回统一 ApiResponse
        return Ok(ApiResponse.Ok(new
        {
            token,
            role,
            accountId = user.AccountId,
            loginName = user.LoginName,
            needChangePassword
        }, "登录成功"));
    }

    /// <summary>
    /// 获取当前用户信息接口
    /// </summary>
    [Authorize]
    [HttpGet("me")]
    public async Task<IActionResult> GetCurrentUser()
    {
        var accountIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(accountIdClaim) || !int.TryParse(accountIdClaim, out int accountId))
            return Unauthorized(ApiResponse.Error(401, "用户未登录"));

        var user = await _context.UserAccounts.FindAsync(accountId);
        if (user == null)
            return NotFound(ApiResponse.Error(404, "用户不存在"));

        // 获取角色
        string role = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value ?? "unknown";

        bool needChangePassword = user.IsFirstLogin == "Y";

        // 宿管负责楼栋（楼长/维修员等有 BuildingId 的管理员），随 me 返回，前端首页展示
        string? buildingId = null;
        string? buildingName = null;
        if (!string.IsNullOrEmpty(user.AdminId))
        {
            var adminBuilding = await _context.Admins
                .Where(admin => admin.AdminId == user.AdminId)
                .Select(admin => new
                {
                    admin.BuildingId,
                    BuildingName = admin.Building != null ? admin.Building.BuildingName : null
                })
                .FirstOrDefaultAsync();

            if (adminBuilding is not null)
            {
                buildingId = adminBuilding.BuildingId?.ToString();
                buildingName = adminBuilding.BuildingName;
            }
        }

        return Ok(ApiResponse.Ok(new
        {
            role,
            accountId = user.AccountId,
            loginName = user.LoginName,
            studentId = user.StudentId,
            adminId = user.AdminId,
            buildingId,
            buildingName,
            needChangePassword
        }, "登录成功"));
    }

    /// <summary>
    /// 退出登录接口
    /// </summary>
    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        // 提取当前 Token 的 jti
        var jti = User.FindFirst(JwtRegisteredClaimNames.Jti)?.Value;
        if (!string.IsNullOrEmpty(jti))
        {
            // 加入黑名单，过期时间 2 小时（与 Token 有效期一致）
            _cache.Set($"revoked_token_{jti}", true, TimeSpan.FromHours(2));
        }

        return Ok(ApiResponse.Ok(new { message = "已退出登录" }));
    }

    /// <summary>
    /// AUTH-04：学生账号注册
    /// </summary>
    [Authorize(Roles = AuthPolicies.SuperAdmin)]
    [HttpPost("accounts/students")]
    public async Task<IActionResult> CreateStudentAccount([FromBody] CreateStudentAccountRequest request)
    {
        request.StudentId = request.StudentId.Trim();
        if (string.IsNullOrWhiteSpace(request.StudentId))
            return BadRequest(ApiResponse.Error(400, "学号不能为空"));

        // 检查学生是否存在
        var student = await _context.Students.FindAsync(request.StudentId);
        if (student == null)
            return BadRequest(ApiResponse.Error(400, "学生不存在"));

        // 检查是否已有账号
        var existing = await _context.UserAccounts.FirstOrDefaultAsync(u => u.StudentId == request.StudentId);
        if (existing != null)
            return BadRequest(ApiResponse.Error(400, "该学生已有账号"));

        // 检查是否输入密码，若为空则生成 8 位随机密码
        var password = string.IsNullOrEmpty(request.Password)
        ? GenerateRandomPassword(8)
        : request.Password;

        // 创建账号
        var loginName = string.IsNullOrWhiteSpace(request.LoginName)
            ? request.StudentId
            : request.LoginName.Trim();
        var loginExists = await _context.UserAccounts.CountAsync(u => u.LoginName == loginName) > 0;
        if (loginExists)
            return BadRequest(ApiResponse.Error(400, "登录名已存在"));

        var account = new UserAccount
        {
            LoginName = loginName,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
            AccountStatus = "正常",
            StudentId = request.StudentId,
            IsFirstLogin = "Y"
        };
        _context.UserAccounts.Add(account);
        await _context.SaveChangesAsync();

        return Ok(ApiResponse.Ok(new
        {
            accountId = account.AccountId,
            loginName = account.LoginName,
            initialPassword = password,
            message = "学生账号创建成功"
        }));
    }

    /// <summary>
    /// AUTH-05：创建宿管账号（需超管权限）
    /// </summary>
    [Authorize(Roles = AuthPolicies.SuperAdmin)]
    [HttpPost("accounts/admins")]
    public async Task<IActionResult> CreateAdminAccount([FromBody] CreateAdminAccountRequest request)
    {
        request.AdminId = request.AdminId.Trim();
        request.Name = request.Name.Trim();
        request.Role = request.Role.Trim();
        if (string.IsNullOrWhiteSpace(request.AdminId) || string.IsNullOrWhiteSpace(request.Name))
            return BadRequest(ApiResponse.Error(400, "管理员编号和姓名不能为空"));

        var validRoles = new[] { "超级管理员", "楼长", "维修员", "辅导员" };
        if (!validRoles.Contains(request.Role))
            return BadRequest(ApiResponse.Error(400, "角色必须为：超级管理员、楼长、维修员、辅导员"));

        if (request.BuildingId.HasValue &&
            await _context.Buildings.CountAsync(building => building.BuildingId == request.BuildingId.Value) == 0)
            return BadRequest(ApiResponse.Error(400, "指定的楼栋不存在"));

        if (await _context.UserAccounts.CountAsync(account => account.LoginName == request.AdminId) > 0)
            return BadRequest(ApiResponse.Error(400, "登录名已存在"));

        // 1. 查找或创建 Admin
        var admin = await _context.Admins.FindAsync(request.AdminId);
        if (admin == null)
        {
            admin = new Admin
            {
                AdminId = request.AdminId,
                AdminName = request.Name,
                RoleLevel = request.Role,
                BuildingId = request.BuildingId,
                Post = request.Post,  // 新增：岗位
            };
            _context.Admins.Add(admin);
        }
        else
        {
            admin.AdminName = request.Name;
            admin.RoleLevel = request.Role;
            admin.BuildingId = request.BuildingId;
            admin.Post = request.Post;  // 新增：更新岗位
        }

        // 2. 检查是否已有账号
        var existing = await _context.UserAccounts.FirstOrDefaultAsync(u => u.AdminId == request.AdminId);
        if (existing != null)
            return BadRequest(ApiResponse.Error(400, "该管理员已有账号"));

        // 3. 创建 UserAccount
        var password = string.IsNullOrEmpty(request.Password) ? GenerateRandomPassword(8) : request.Password;
        var account = new UserAccount
        {
            LoginName = request.AdminId, // 可自定义，暂用 AdminId
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
            AccountStatus = "正常",
            AdminId = request.AdminId,
            IsFirstLogin = "Y"
        };
        _context.UserAccounts.Add(account);

        await _context.SaveChangesAsync();

        // 4. 审计日志（包含岗位信息）
        await _auditService.LogEventAsync(
            eventType: "POST /accounts/admins",
            targetType: "Admin",
            targetId: request.AdminId,
            actorAccountId: GetCurrentUserId(),
            details: $"创建宿管账号：{account.LoginName}，角色：{admin.RoleLevel}，楼栋ID：{admin.BuildingId}，岗位：{admin.Post}"
        );

        return Ok(ApiResponse.Ok(new
        {
            accountId = account.AccountId,
            loginName = account.LoginName,
            initialPassword = password,
            message = "宿管账号创建成功"
        }));
    }

    /// <summary>
    /// PWD-01：修改密码（用户自助修改，首登强制）
    /// </summary>
    [Authorize]
    [HttpPut("password")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
    {
        var accountIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(accountIdClaim) || !int.TryParse(accountIdClaim, out int accountId))
            return Unauthorized(ApiResponse.Error(401, "用户未登录"));

        var user = await _context.UserAccounts.FindAsync(accountId);
        if (user == null)
            return NotFound(ApiResponse.Error(404, "用户不存在"));

        // 验证旧密码
        if (!BCrypt.Net.BCrypt.Verify(request.OldPassword, user.PasswordHash))
            return BadRequest(ApiResponse.Error(400, "旧密码错误"));

        // ---- 新密码强度校验 ----
        if (request.NewPassword.Length < 8)
            return BadRequest(ApiResponse.Error(400, "新密码长度至少8位"));

        if (!request.NewPassword.Any(char.IsLetter) || !request.NewPassword.Any(char.IsDigit))
            return BadRequest(ApiResponse.Error(400, "新密码必须包含字母和数字"));

        // 可选：禁止连续字符、禁止常见密码等，可后续扩展

        // 更新密码
        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
        await _userAccountService.UpdateIsFirstLoginAsync(accountId, "N");

        // 审计日志（可选）
        await _auditService.LogEventAsync(
            eventType: "PUT /auth/password",
            targetType: "UserAccount",
            targetId: accountId.ToString(),
            actorAccountId: accountId,
            details: "用户修改密码"
        );

        return Ok(ApiResponse.Ok(new { message = "密码修改成功" }));
    }

    // 工具函数
    private static string GenerateRandomPassword(int length)
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghjkmnpqrstuvwxyz23456789";
        return new string(Enumerable.Repeat(chars, length)
            .Select(s => s[RandomNumberGenerator.GetInt32(s.Length)]).ToArray());
    }
    private int? GetCurrentUserId()
    {
        var claim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        return int.TryParse(claim, out var id) ? id : (int?)null;
    }
}
