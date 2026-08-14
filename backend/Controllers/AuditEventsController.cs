using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TemplateDormApi.Data;
using TemplateDormApi.DTO;
using TemplateDormApi.Security;
using TemplateDormApi.Services;

namespace TemplateDormApi.Controllers;

[ApiController]
[Authorize(Roles = AuthPolicies.SuperAdmin)]
[Route("audit-events")]
public class AuditEventsController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IAuditService _auditService;
    private readonly ILogger<AuditEventsController> _logger;

    public AuditEventsController(
        AppDbContext context,
        IAuditService auditService,
        ILogger<AuditEventsController> logger)
    {
        _context = context;
        _auditService = auditService;
        _logger = logger;
    }

    // GET /audit-events - 审计日志查询（SUPER-07）
    [HttpGet]
    public async Task<IActionResult> GetAuditEvents([FromQuery] AuditEventQueryDto query)
    {
        // 构建查询
        var baseQuery = _context.AuditEvents
            .Include(a => a.Actor)
            .AsNoTracking()
            .AsQueryable();

        // 过滤条件
        if (!string.IsNullOrEmpty(query.EventType))
            baseQuery = baseQuery.Where(a => a.EventType == query.EventType);

        if (!string.IsNullOrEmpty(query.TargetType))
            baseQuery = baseQuery.Where(a => a.TargetType == query.TargetType);

        if (!string.IsNullOrEmpty(query.TargetId))
            baseQuery = baseQuery.Where(a => a.TargetId == query.TargetId);

        if (query.ActorAccountId.HasValue)
            baseQuery = baseQuery.Where(a => a.ActorAccountId == query.ActorAccountId);

        if (query.StartTime.HasValue)
            baseQuery = baseQuery.Where(a => a.EventTime >= query.StartTime.Value);

        if (query.EndTime.HasValue)
            baseQuery = baseQuery.Where(a => a.EventTime <= query.EndTime.Value);

        // 分页参数
        int page = query.Page ?? 1;
        int pageSize = query.PageSize ?? 20;
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 20;
        if (pageSize > 100) pageSize = 100;

        // 总记录数
        var totalCount = await baseQuery.CountAsync();

        // 获取分页数据
        var items = await baseQuery
            .OrderByDescending(a => a.EventTime)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(a => new
            {
                a.AuditId,
                a.EventTime,
                a.EventType,
                a.TargetType,
                a.TargetId,
                a.Details,
                ActorAccountId = a.ActorAccountId,
                ActorLoginName = a.Actor != null ? a.Actor.LoginName : null
            })
            .ToListAsync();

        var result = new PagedResult<object>
        {
            Items = items.Cast<object>().ToList(),
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };

        // 以下代码会将查询审计日志同样计入在审计日志中，可能导致日志累赘，若后续确认有必要计入，可以取消注释
        /* 
        await _auditService.LogEventAsync(
            eventType: "GET /audit-events",
            targetType: "AuditEvent",
            actorAccountId: GetCurrentUserId()
        );
        */

        return Ok(ApiResponse.Ok(result));
    }

    // ===== DTO 定义 =====
    public class AuditEventQueryDto
    {
        public string? EventType { get; set; }
        public string? TargetType { get; set; }
        public string? TargetId { get; set; }
        public int? ActorAccountId { get; set; }
        public DateTime? StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public int? Page { get; set; } = 1;
        public int? PageSize { get; set; } = 20;
    }

    public class PagedResult<T>
    {
        public List<T> Items { get; set; } = new();
        public int TotalCount { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
    }

    // ===== 辅助方法 =====
    private int? GetCurrentUserId()
    {
        var claim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        return int.TryParse(claim, out var id) ? id : null;
    }
}