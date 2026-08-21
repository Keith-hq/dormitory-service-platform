using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TemplateDormApi.Data;
using TemplateDormApi.DTO;
using TemplateDormApi.Exceptions;
using TemplateDormApi.Models;
using TemplateDormApi.Security;
using TemplateDormApi.Services;

namespace TemplateDormApi.Controllers;

/// <summary>
/// 访客授权接口（STU-32/33/34/41）。路由对齐 Apifox 契约。
/// </summary>
[ApiController]
[Route("api")]
public class VisitorController : ControllerBase
{
    private readonly IVisitorService _service;
    private readonly AppDbContext _context;
    private readonly IStudentIdentityService _identityService;

    public VisitorController(
        IVisitorService service,
        AppDbContext context,
        IStudentIdentityService identityService)
    {
        _service = service;
        _context = context;
        _identityService = identityService;
    }

    /// <summary>从 JWT 解析当前学生的 Student_ID。</summary>
    private async Task<string> ResolveStudentId()
    {
        var accountId = CurrentUser.GetAccountId(User)
            ?? throw new BusinessException(401, "未登录或 Token 无效");

        var studentId = await _context.UserAccounts
            .Where(a => a.AccountId == accountId)
            .Select(a => a.StudentId)
            .FirstOrDefaultAsync();

        return studentId ?? throw new BusinessException(401, "当前账户未关联学生身份");
    }

    /// <summary>校验路径 studentId 属于当前登录学生。</summary>
    private async Task EnsureOwnStudentId(string studentId)
    {
        var accountId = CurrentUser.GetAccountId(User)
            ?? throw new BusinessException(401, "未登录或 Token 无效");
        await _identityService.EnsureOwnStudentIdAsync(accountId, studentId, HttpContext.RequestAborted);
    }

    /// <summary>STU-32 申请访客授权（限时）。</summary>
    [HttpPost("visitor-authorizations")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<VisitorAuthorization>>> Apply(
        [FromBody] VisitorApplyRequest request)
    {
        var studentId = await ResolveStudentId();
        var auth = await _service.ApplyAsync(studentId, request);
        return Ok(ApiResponse.Created(auth));
    }

    /// <summary>STU-33 查询我的访客授权记录。</summary>
    [HttpGet("students/{studentId}/visitor-authorizations")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<PagedResult<VisitorAuthorization>>>> MyList(
        string studentId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        await EnsureOwnStudentId(studentId);
        if (page < 1 || pageSize < 1 || pageSize > 100)
            return BadRequest(ApiResponse.Error(400, "分页参数不合法：page >= 1，1 <= pageSize <= 100"));

        var result = await _service.GetMyListAsync(studentId, page, pageSize);
        return Ok(ApiResponse.Ok(result));
    }

    /// <summary>STU-34 查看授权凭证（Token）。</summary>
    [HttpGet("visitor-authorizations/{authId:int}")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<VisitorAuthorization>>> Credential(int authId)
    {
        var studentId = await ResolveStudentId();
        var auth = await _service.GetCredentialAsync(authId, studentId);
        return Ok(ApiResponse.Ok(auth));
    }

    /// <summary>STU-41 撤销授权。</summary>
    [HttpPost("visitor-authorizations/{authId:int}/revoke")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<VisitorAuthorization>>> Revoke(int authId)
    {
        var studentId = await ResolveStudentId();
        var auth = await _service.RevokeAsync(authId, studentId);
        return Ok(ApiResponse.Ok(auth, "撤销成功"));
    }
}
