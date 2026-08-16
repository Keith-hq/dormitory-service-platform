using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using TemplateDormApi.Models;
using TemplateDormApi.Security;

namespace TemplateDormApi.Services;

/// JWT 生成服务接口
public interface IJwtService
{
    /// 为用户生成 JWT 令牌
    /// <param name="user">用户账户信息</param>
    /// <param name="role">角色（小写，符合 AuthPolicies 常量定义）</param>
    /// <returns>JWT 字符串</returns>
    string GenerateToken(UserAccount user, string role);
}

/// JWT 生成服务实现
public class JwtService : IJwtService
{
    private readonly IConfiguration _configuration;

    public JwtService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public string GenerateToken(UserAccount user, string role)
    {
        // 从配置读取 JWT 参数
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]!));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        // 构建 Claims（包含角色）
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, user.AccountId.ToString()),
            new Claim(ClaimTypes.Name, user.LoginName),
            new Claim(ClaimTypes.Role, role)
        };

        var token = new JwtSecurityToken(
            issuer: _configuration["Jwt:Issuer"],
            audience: _configuration["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddHours(2), // 有效期 2 小时
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}