using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TemplateDormApi.Data;
using TemplateDormApi.DTO;
using TemplateDormApi.Exceptions;
using TemplateDormApi.Security;
using TemplateDormApi.Services;

namespace TemplateDormApi.Controllers;

[ApiController]
[Route("api")]
public class CleaningRequestController : ControllerBase
{
    private readonly ICleaningRequestService _service;
    private readonly AppDbContext _context;

    public CleaningRequestController(ICleaningRequestService service, AppDbContext context)
    {
        _service = service;
        _context = context;
    }

    private async Task<string> ResolveStudentId()
    {
        var accountId = CurrentUser.GetAccountId(User)
            ?? throw new BusinessException(401, "未登录或 Token 无效");
        var studentId = await _context.UserAccounts.AsNoTracking()
            .Where(a => a.AccountId == accountId)
            .Select(a => a.StudentId)
            .FirstOrDefaultAsync();
        return studentId ?? throw new BusinessException(401, "当前账户未关联学生身份");
    }

    /// <summary>学生：为当前在住房间申请保洁。</summary>
    [HttpPost("cleaning-requests")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<CleaningRequestDto>>> Apply(
        [FromBody] ApplyCleaningRequest request,
        CancellationToken cancellationToken)
    {
        var studentId = await ResolveStudentId();
        var result = await _service.ApplyStudentAsync(studentId, request?.Reason, cancellationToken);
        return Ok(ApiResponse.Ok(result, "保洁申请已提交，等待宿管处理"));
    }

    /// <summary>宿管：保洁任务队列（宿舍申请 / 楼栋整体 / 设施触发）。</summary>
    [HttpGet("cleaning-requests")]
    [Authorize(Policy = AuthPolicies.DormAdmin)]
    public async Task<ActionResult<ApiResponse<PagedResult<CleaningRequestDto>>>> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await _service.GetListAsync(page, pageSize, cancellationToken);
        return Ok(ApiResponse.Ok(result));
    }

    /// <summary>学生：查看我的保洁申请记录。</summary>
    [HttpGet("cleaning-requests/my")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<CleaningRequestDto>>>> Mine(
        CancellationToken cancellationToken)
    {
        var studentId = await ResolveStudentId();
        var result = await _service.GetMineAsync(studentId, cancellationToken);
        return Ok(ApiResponse.Ok(result));
    }

    /// <summary>宿管：完成保洁任务（幂等）。</summary>
    [HttpPut("cleaning-requests/{ticketId:long}/complete")]
    [Authorize(Policy = AuthPolicies.DormAdmin)]
    public async Task<ActionResult<ApiResponse<CleaningRequestDto>>> Complete(
        long ticketId,
        CancellationToken cancellationToken)
    {
        var result = await _service.CompleteAsync(ticketId, cancellationToken);
        return Ok(ApiResponse.Ok(result, "保洁任务已完成"));
    }
}
