using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using TemplateDormApi.Models;
using TemplateDormApi.Security;

namespace TemplateDormApi.Services;

/// JWT 生成服务实现
public class JwtService : IJwtService
{
    private readonly IConfiguration _configuration;

    public JwtService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public async Task<string> GenerateToken(UserAccount user, string role)
    {
        // 从配置读取 JWT 参数
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]!));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        int tokenVersion = 0;

        // 构建 Claims（包含角色）
        var claims = new List<Claim>
        {
            new Claim("TokenVersion", tokenVersion.ToString()),
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