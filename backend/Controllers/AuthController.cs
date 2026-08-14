using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TemplateDormApi.Data;
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

    public AuthController(AppDbContext context, IJwtService jwtService)
    {
        _context = context;
        _jwtService = jwtService;
    }

    /// 登录请求 DTO
    public class LoginRequest
    {
        public string LoginName { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }

    /// <summary>
    /// 用户登录接口
    /// </summary>
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        // 1. 根据登录名查找用户
        var user = await _context.UserAccounts
            .FirstOrDefaultAsync(u => u.LoginName == request.LoginName);

        // 密码校验（此处为明文比对，后续应改为哈希验证）
        if (user == null || user.PasswordHash != request.Password)
            return Unauthorized(new { message = "用户名或密码错误" });

        // 2. 推导角色
        string role;
        if (!string.IsNullOrEmpty(user.StudentId))
        {
            role = AuthPolicies.Student; // "student"
        }
        else if (!string.IsNullOrEmpty(user.AdminId))
        {
            // 从 Admin 表查询角色等级
            var admin = await _context.Admins.FindAsync(user.AdminId);
            role = admin?.RoleLevel ?? "unknown";
        }
        else
        {
            role = "unknown";
        }

        // 3. 生成 JWT
        var token = _jwtService.GenerateToken(user, role);

        // 4. 返回结果
        return Ok(new
        {
            token,
            role,
            accountId = user.AccountId,
            loginName = user.LoginName
        });
    }
}