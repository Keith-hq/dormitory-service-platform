using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using TemplateDormApi.Models;
using TemplateDormApi.Repository;
using TemplateDormApi.Security;

namespace TemplateDormApi.Services;

/// JWT 生成服务实现
public class JwtService : IJwtService
{
    private readonly IConfiguration _configuration;
    private readonly AdminRepository _adminRepository;

    public JwtService(IConfiguration configuration, AdminRepository adminRepository)
    {
        _configuration = configuration;
        _adminRepository = adminRepository;
    }

    public async Task<string> GenerateToken(UserAccount user, string role)
    {
        // 获取 TokenVersion
        int tokenVersion = 0;
        if (!string.IsNullOrEmpty(user.AdminId))
        {
            tokenVersion = await _adminRepository.GetTokenVersionAsync(user.AdminId);
        }

        var claims = new List<Claim>
    {
        new Claim(ClaimTypes.NameIdentifier, user.AccountId.ToString()),
        new Claim(ClaimTypes.Name, user.LoginName),
        new Claim(ClaimTypes.Role, role),
        new Claim("TokenVersion", tokenVersion.ToString()),
        new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())  // 新增 jti
    };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]!));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _configuration["Jwt:Issuer"],
            audience: _configuration["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddHours(2),
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}