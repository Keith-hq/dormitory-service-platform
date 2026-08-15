using Microsoft.AspNetCore.Http;
using System.Security.Claims;
using TemplateDormApi.Data;
using TemplateDormApi.Models;

namespace TemplateDormApi.Services;

public class AuditService : IAuditService
{
    private readonly AppDbContext _context;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public AuditService(AppDbContext context, IHttpContextAccessor httpContextAccessor)
    {
        _context = context;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task LogEventAsync(
        string eventType,
        string? targetType = null,
        string? targetId = null,
        int? actorAccountId = null,
        DateTime? eventTime = null,
        string? details = null)  // 新增参数
    {
        // 如果未提供操作人，从当前 HttpContext 获取
        if (actorAccountId == null)
        {
            var httpContext = _httpContextAccessor.HttpContext;
            var userIdClaim = httpContext?.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (int.TryParse(userIdClaim, out var id))
                actorAccountId = id;
        }

        var auditEvent = new AuditEvent
        {
            ActorAccountId = actorAccountId,
            EventType = eventType,
            TargetType = targetType,
            TargetId = targetId,
            EventTime = eventTime ?? DateTime.Now,
            Details = details  // 赋值 details
        };

        _context.AuditEvents.Add(auditEvent);
        await _context.SaveChangesAsync();
    }
}