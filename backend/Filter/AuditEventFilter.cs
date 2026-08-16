using Microsoft.AspNetCore.Mvc.Filters;
using TemplateDormApi.Services;

namespace TemplateDormApi.Filters;

public class AuditEventFilter : IAsyncActionFilter
{
    private readonly IAuditService _auditService;

    public AuditEventFilter(IAuditService auditService)
    {
        _auditService = auditService;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var resultContext = await next();

        var method = context.HttpContext.Request.Method;
        if (method == "GET")
        {
            // 若需要记录关键 GET 请求，可在此处添加条件，暂略
            return;
        }

        var user = context.HttpContext.User;
        var userIdClaim = user.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        var userId = int.TryParse(userIdClaim, out var id) ? id : (int?)null;
        var userName = user.Identity?.Name;

        await _auditService.LogEventAsync(
            eventType: $"{method} {context.HttpContext.Request.Path}",
            targetType: "Api",
            targetId: context.HttpContext.Request.Path,
            actorAccountId: userId
        );
    }
}