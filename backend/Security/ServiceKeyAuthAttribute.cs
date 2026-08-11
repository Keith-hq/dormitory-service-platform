using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using TemplateDormApi.DTO;

namespace TemplateDormApi.Security;

/// <summary>
/// 保护内部服务接口的共享密钥验证属性。
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class ServiceKeyAuthAttribute : TypeFilterAttribute
{
    public ServiceKeyAuthAttribute() : base(typeof(ServiceKeyAuthorizationFilter))
    {
    }
}

/// <summary>
/// 由 TypeFilter 创建，以便从依赖注入容器获取配置。
/// </summary>
public sealed class ServiceKeyAuthorizationFilter : IAuthorizationFilter
{
    private const string HeaderName = "X-Service-Key";
    private readonly IConfiguration _configuration;

    public ServiceKeyAuthorizationFilter(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public void OnAuthorization(AuthorizationFilterContext context)
    {
        var expectedServiceKey = _configuration["ServiceKey:Shared"];
        var headerValues = context.HttpContext.Request.Headers[HeaderName];

        if (string.IsNullOrWhiteSpace(expectedServiceKey) ||
            headerValues.Count != 1 ||
            string.IsNullOrWhiteSpace(headerValues[0]) ||
            !string.Equals(headerValues[0], expectedServiceKey, StringComparison.Ordinal))
        {
            context.Result = new UnauthorizedObjectResult(ApiResponse.Error(401, "serviceKey 无效"));
        }
    }
}
