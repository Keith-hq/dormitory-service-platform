using System.Security.Claims;
using TemplateDormApi.Data;

namespace TemplateDormApi.Middleware;

public class FirstLoginMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IHostEnvironment _env;

    public FirstLoginMiddleware(RequestDelegate next, IHostEnvironment env)
    {
        _next = next;
        _env = env;
    }

    public async Task InvokeAsync(HttpContext context, AppDbContext dbContext)
    {
        // 测试环境绕过中间项
        if (_env.IsEnvironment("Test"))
        {
            await _next(context);
            return;
        }

        var user = context.User;
        if (user.Identity?.IsAuthenticated == true)
        {
            var accountIdClaim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (int.TryParse(accountIdClaim, out var accountId))
            {
                var userAccount = await dbContext.UserAccounts.FindAsync(accountId);
                if (userAccount?.IsFirstLogin == "Y")
                {
                    var path = context.Request.Path.Value ?? "";
                    var allowedPaths = new[] { "/api/auth/password", "/api/auth/me", "/api/auth/logout", "/api/auth/captcha" };
                    if (!allowedPaths.Any(p => path.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
                    {
                        context.Response.StatusCode = 403;
                        await context.Response.WriteAsync("{\"code\":403,\"message\":\"首次登录请先修改密码\",\"data\":null}");
                        return;
                    }
                }
            }
        }

        await _next(context);
    }
}