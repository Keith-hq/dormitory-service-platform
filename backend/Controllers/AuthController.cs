using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using System.Drawing;
using System.Drawing.Imaging;
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

    public AuthController(AppDbContext context, IJwtService jwtService, IMemoryCache cache, ILogger<AuthController> logger)
    {
        _context = context;
        _jwtService = jwtService;
        _cache = cache;
        _logger = logger;
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
        public string Password { get; set; } = string.Empty;
    }

    public class CreateAdminAccountRequest
    {
        public string AdminId { get; set; } = string.Empty;
        public string? LoginName { get; set; }
        public string Password { get; set; } = string.Empty;
    }

    /// <summary>
    // 修改密码请求 DTO
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
        // 1. 生成 4 位数字验证码
        var code = new Random().Next(1000, 9999).ToString();
        var captchaId = Guid.NewGuid().ToString("N");

        // 2. 存储到内存缓存（有效期 5 分钟）
        _cache.Set(captchaId, code, TimeSpan.FromMinutes(5));

        // 3. 创建图片并绘制验证码
        using var bitmap = new Bitmap(200, 80);
        using var g = Graphics.FromImage(bitmap);
        g.Clear(Color.White);

        // 使用系统字体（Windows 下可用）
        using var font = new Font("Arial", 36, FontStyle.Bold);
        using var brush = new SolidBrush(Color.Black);
        g.DrawString(code, font, brush, 20, 20);

        // 添加干扰线
        var rand = new Random();
        for (int i = 0; i < 3; i++)
        {
            using var pen = new Pen(Color.FromArgb(rand.Next(100, 200), rand.Next(100, 200), rand.Next(100, 200)), 2);
            g.DrawLine(pen, rand.Next(0, 200), rand.Next(0, 80), rand.Next(0, 200), rand.Next(0, 80));
        }

        // 4. 输出 PNG 流
        using var ms = new MemoryStream();
        bitmap.Save(ms, ImageFormat.Png);
        ms.Seek(0, SeekOrigin.Begin);

        // 5. 返回 CaptchaId 和图片
        Response.Headers.Add("X-Captcha-Id", captchaId);
        return File(ms.ToArray(), "image/png");
    }

    /// <summary>
    /// 用户登录接口
    /// </summary>
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        // 1. 验证码校验
        if (string.IsNullOrEmpty(request.CaptchaId) || string.IsNullOrEmpty(request.CaptchaCode))
        {
            return BadRequest(ApiResponse.Error(400, "请提供验证码"));
        }

        var storedCode = _cache.Get<string>(request.CaptchaId);
        if (storedCode == null || !storedCode.Equals(request.CaptchaCode, StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(ApiResponse.Error(400, "验证码错误或已过期"));
        }

        _cache.Remove(request.CaptchaId);

        // 2. 用户验证
        var user = await _context.UserAccounts
            .FirstOrDefaultAsync(u => u.LoginName == request.LoginName);

        if (user == null)
            return Unauthorized(ApiResponse.Error(401, "用户名或密码错误"));

        // 3. 检查 AccountStatus
        if (user.AccountStatus != "正常")
            return Unauthorized(ApiResponse.Error(401, "账号已停用，请联系管理员"));

        // 4. 密码验证
        if (!BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            return Unauthorized(ApiResponse.Error(401, "用户名或密码错误"));

        // 5. 角色映射（按 C-038 裁决）
        string role;
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
                "维修员" => "repair_staff",
                _ => null
            };

            if (string.IsNullOrEmpty(role))
                return Unauthorized(ApiResponse.Error(401, "角色权限异常，请联系管理员"));
        }
        else
        {
            return Unauthorized(ApiResponse.Error(401, "用户身份异常"));
        }

        // 6. 生成 Token
        var token = _jwtService.GenerateToken(user, role);

        // 7. 返回统一 ApiResponse
        var responseData = new
        {
            token,
            role,
            accountId = user.AccountId,
            loginName = user.LoginName
        };

        return Ok(ApiResponse.Ok(responseData, "登录成功"));
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

        // 创建账号
        var account = new UserAccount
        {
            LoginName = request.LoginName ?? request.StudentId,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            AccountStatus = "正常",
            StudentId = request.StudentId
        };
        _context.UserAccounts.Add(account);
        await _context.SaveChangesAsync();

        return Ok(ApiResponse.Ok(new
        {
            accountId = account.AccountId,
            loginName = account.LoginName,
            message = "学生账号创建成功"
        }));
    }

    /// <summary>
    /// AUTH-05：创建宿管账号（需超管权限）
    /// </summary>
    [Authorize(Roles = "super_admin")]
    [HttpPost("accounts/admins")]
    public async Task<IActionResult> CreateAdminAccount([FromBody] CreateAdminAccountRequest request)
    {
        // 检查管理员是否存在
        var admin = await _context.Admins.FindAsync(request.AdminId);
        if (admin == null)
            return BadRequest(ApiResponse.Error(400, "管理员不存在"));

        // 检查是否已有账号
        var existing = await _context.UserAccounts.FirstOrDefaultAsync(u => u.AdminId == request.AdminId);
        if (existing != null)
            return BadRequest(ApiResponse.Error(400, "该管理员已有账号"));

        // 创建账号
        var account = new UserAccount
        {
            LoginName = request.LoginName ?? request.AdminId,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            AccountStatus = "正常",
            AdminId = request.AdminId
        };
        _context.UserAccounts.Add(account);
        await _context.SaveChangesAsync();

        // 临时审计：使用日志记录（后续改用 D_Audit_Event 表）
        var currentUser = User.Identity?.Name ?? "未知";
        _logger.LogInformation($"超管 {currentUser} 创建了宿管账号 {account.LoginName}，AdminId={request.AdminId}");

        return Ok(ApiResponse.Ok(new
        {
            accountId = account.AccountId,
            loginName = account.LoginName,
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

        // 更新密码
        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
        await _context.SaveChangesAsync();

        return Ok(ApiResponse.Ok(new { message = "密码修改成功" }));
    }
}