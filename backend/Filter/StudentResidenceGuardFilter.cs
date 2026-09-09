using System.Security.Claims;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using TemplateDormApi.Data;
using TemplateDormApi.DTO;

namespace TemplateDormApi.Filters;

/// <summary>
/// 学生“在住”门槛（全局，仅拦截明确的宿舍行为写接口）：
/// 退宿后(无在住分配)学生不能再用 充值/缴费/设施预约与使用/共享物品借用/发起报修/申请访客码；
/// 其余(查看历史、改联系方式、撤销/归还/取消、离校申请等)放行，避免误伤归属校验等场景。
/// 仅对 角色=student 且命中 GuardedPaths 的写请求校验；宿管/维修/辅导员等不受影响。
/// </summary>
public sealed class StudentResidenceGuardFilter : IAsyncActionFilter
{
    private readonly AppDbContext _context;

    // 需要“在住”身份的宿舍行为写接口（动态 id 段用 [^/]+）
    private static readonly Regex[] GuardedPaths =
    {
        new(@"^/api/wallet/recharges$", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"^/api/wallet/payments$", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"^/api/facility-bookings(/[^/]+/(start|finish))?$", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"^/api/item-loans$", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"^/api/repair-tickets$", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"^/api/visitor-authorizations$", RegexOptions.IgnoreCase | RegexOptions.Compiled)
    };

    public StudentResidenceGuardFilter(AppDbContext context)
    {
        _context = context;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var request = context.HttpContext.Request;
        if (request.Method is "GET" or "HEAD" or "OPTIONS")
        {
            await next();
            return;
        }

        var user = context.HttpContext.User;
        if (user.FindFirst(ClaimTypes.Role)?.Value != "student")
        {
            await next();
            return;
        }

        var path = request.Path.Value ?? string.Empty;
        var guarded = false;
        foreach (var pattern in GuardedPaths)
        {
            if (pattern.IsMatch(path))
            {
                guarded = true;
                break;
            }
        }
        if (!guarded)
        {
            await next();
            return;
        }

        var accountIdClaim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!int.TryParse(accountIdClaim, out var accountId))
        {
            await next(); // 交后续鉴权处理
            return;
        }

        var studentId = await _context.UserAccounts.AsNoTracking()
            .Where(a => a.AccountId == accountId)
            .Select(a => a.StudentId)
            .FirstOrDefaultAsync();

        if (string.IsNullOrWhiteSpace(studentId))
        {
            await next();
            return;
        }

        // Oracle EF 顶层 AnyAsync → ORA-00904，用 CountAsync（与 AllocationService 同源处理）
        var active = await _context.BedAllocations.AsNoTracking()
            .CountAsync(a => a.StudentId == studentId && a.CheckOutDate == null) > 0;

        if (!active)
        {
            context.Result = new OkObjectResult(
                ApiResponse.Error(409, "已退宿，账号暂不可用，请由宿管重新分配住宿后再使用"));
            return;
        }

        await next();
    }
}
