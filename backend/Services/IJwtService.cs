using TemplateDormApi.DTO;
using TemplateDormApi.Models;

namespace TemplateDormApi.Services;

/// JWT 生成服务接口
public interface IJwtService
{
    /// 为用户生成 JWT 令牌
    /// <param name="user">用户账户信息</param>
    /// <param name="role">角色（小写，符合 AuthPolicies 常量定义）</param>
    /// <returns>JWT 字符串</returns>
    Task<string> GenerateToken(UserAccount user, string role);
}
