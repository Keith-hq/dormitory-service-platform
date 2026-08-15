using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using TemplateDormApi.Data;
using TemplateDormApi.DTO;
using TemplateDormApi.Models;
using TemplateDormApi.Security;
using TemplateDormApi.Services;
using SkiaSharp;
using System.Linq;

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
    private readonly IHostEnvironment _env;

    public AuthController(AppDbContext context, IJwtService jwtService, IMemoryCache cache, ILogger<AuthController> logger, IAuditService auditService, IHostEnvironment env)
    {
        _context = context;
        _jwtService = jwtService;
        _cache = cache;
        _logger = logger;
        _auditService = auditService;
        _env = env;
    }

    /// <summary>
    /// 登录请求 DTO
    /// </summary>
    public class LoginRequest
    {
        public string LoginName { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string CaptchaId { get; set; } = string.Empty;
        public string CaptchaCode { get; set; } = string.Empty;
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
        public int BuildingId { get; set; }                 // required
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
    /// CAPTCHA-01：获取图形验证码
    /// </summary>
    [HttpGet("captcha")]
    public IActionResult GetCaptcha()
    {
        var code = new Random().Next(1000, 9999).ToString();
        var captchaId = Guid.NewGuid().ToString("N");
        _cache.Set(captchaId, code, TimeSpan.FromMinutes(5));

        using var bitmap = new SKBitmap(200, 80);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.White);

        // 创建字体和画笔
        using var typeface = SKTypeface.FromFamilyName("Arial", SKFontStyleWeight.Normal, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright);
        using var font = new SKFont(typeface, 36f);  // 字体大小
        using var paint = new SKPaint
        {
            Color = SKColors.Black,
            IsAntialias = true
        };

        // 绘制验证码文字 (坐标 x=20, y=55)
        canvas.DrawText(code, 20, 55, SKTextAlign.Left, font, paint);

        // 添加干扰线
        var rand = new Random();
        for (int i = 0; i < 3; i++)
        {
            using var linePaint = new SKPaint
            {
                Color = new SKColor((byte)rand.Next(100, 200), (byte)rand.Next(100, 200), (byte)rand.Next(100, 200)),
                StrokeWidth = 2,
                IsAntialias = true
            };
            canvas.DrawLine(rand.Next(0, 200), rand.Next(0, 80), rand.Next(0, 200), rand.Next(0, 80), linePaint);
        }

        // 输出 PNG
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        var bytes = data.ToArray();

        Response.Headers["X-Captcha-Id"] = captchaId;
        return File(bytes, "image/png");
    }

    /// <summary>
    /// 用户登录接口
    /// </summary>
    // TODO: 当前未实现首次登录强制修改密码。
    // 后续需在 D_USER_ACCOUNT 表增加 IS_FIRST_LOGIN VARCHAR2(1) DEFAULT 'Y'，
    // 并在登录响应中返回 needChangePassword = true，由前端引导跳转。
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

        // 3. 验证码逻辑（测试环境跳过）
        int failCount = 0;
        bool requireCaptcha = false;

        if (!_env.IsEnvironment("Test"))  // 测试环境跳过验证码
        {
            var cacheKey = $"login_fail_{request.LoginName}";
            failCount = _cache.Get<int?>(cacheKey) ?? 0;
            requireCaptcha = failCount >= 3;

            if (requireCaptcha)
            {
                if (string.IsNullOrEmpty(request.CaptchaId) || string.IsNullOrEmpty(request.CaptchaCode))
                    return BadRequest(ApiResponse.Error(400, "请提供验证码"));

                var storedCode = _cache.Get<string>(request.CaptchaId);
                if (storedCode == null || !storedCode.Equals(request.CaptchaCode, StringComparison.OrdinalIgnoreCase))
                {
                    // 验证码错误：增加失败计数，不直接判登录失败
                    _cache.Set(cacheKey, failCount + 1, TimeSpan.FromHours(1));
                    return BadRequest(ApiResponse.Error(400, "验证码错误或已过期"));
                }

                _cache.Remove(request.CaptchaId);
            }
        }

        // 4. 密码验证
        if (!BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
        {
            // 密码错误：增加失败计数（仅非测试环境）
            if (!_env.IsEnvironment("Test"))
                _cache.Set($"login_fail_{request.LoginName}", failCount + 1, TimeSpan.FromHours(1));
            return Unauthorized(ApiResponse.Error(401, "用户名或密码错误"));
        }

        // 5. 登录成功：重置失败计数（仅非测试环境）
        if (!_env.IsEnvironment("Test"))
            _cache.Remove($"login_fail_{request.LoginName}");

        // 6. 角色映射（按 C-038 裁决）
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
                "维修员" => "repairman",   // 与 develop 策略一致
                _ => null
            };

            if (string.IsNullOrEmpty(role))
                return Unauthorized(ApiResponse.Error(401, "角色权限异常，请联系管理员"));
        }
        else
        {
            return Unauthorized(ApiResponse.Error(401, "用户身份异常"));
        }

        // 7. 生成 Token
        var token = _jwtService.GenerateToken(user, role);

        // 8. 返回统一 ApiResponse
        return Ok(ApiResponse.Ok(new
        {
            token,
            role,
            accountId = user.AccountId,
            loginName = user.LoginName
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

        return Ok(ApiResponse.Ok(new
        {
            accountId = user.AccountId,
            loginName = user.LoginName,
            role = role,
            studentId = user.StudentId,
            adminId = user.AdminId
        }));
    }

    /// <summary>
    /// 退出登录接口
    /// </summary>
    [Authorize]
    [HttpPost("logout")]
    public IActionResult Logout()
    {
        return Ok(ApiResponse.Ok(new { message = "已退出登录" }));
    }
    /// <summary>
    /// AUTH-04：学生账号注册
    /// </summary>
    [HttpPost("accounts/students")]
    public async Task<IActionResult> CreateStudentAccount([FromBody] CreateStudentAccountRequest request)
    {
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
        var account = new UserAccount
        {
            LoginName = request.LoginName ?? request.StudentId,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
            AccountStatus = "正常",
            StudentId = request.StudentId
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
                Post = request.Post  // 新增：岗位
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
            AdminId = request.AdminId
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
        await _context.SaveChangesAsync();

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
    private string GenerateRandomPassword(int length)
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghjkmnpqrstuvwxyz23456789";
        var random = new Random();
        return new string(Enumerable.Repeat(chars, length)
            .Select(s => s[random.Next(s.Length)]).ToArray());
    }
    private int? GetCurrentUserId()
    {
        var claim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        return int.TryParse(claim, out var id) ? id : (int?)null;
    }
}