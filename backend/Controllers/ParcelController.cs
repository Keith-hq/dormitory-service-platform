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
/// 快递接口（PKG-01/02）。路由对齐 Apifox 契约。
/// </summary>
[ApiController]
[Route("api")]
public class ParcelController : ControllerBase
{
    private readonly IParcelService _service;
    private readonly AppDbContext _context;
    private readonly IStudentIdentityService _identityService;

    public ParcelController(
        IParcelService service,
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

    /// <summary>PKG-01 查询我的快递（取件码）。</summary>
    [HttpGet("students/{studentId}/packages")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<PagedResult<ParcelRecord>>>> MyParcels(
        string studentId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        await EnsureOwnStudentId(studentId);
        if (page < 1 || pageSize < 1 || pageSize > 100)
            return BadRequest(ApiResponse.Error(400, "分页参数不合法：page >= 1，1 <= pageSize <= 100"));

        var result = await _service.GetMyParcelsAsync(studentId, page, pageSize);
        return Ok(ApiResponse.Ok(result));
    }

    /// <summary>PKG-02 确认取件。</summary>
    [HttpPost("packages/{packageId:int}/pickup")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<ParcelRecord>>> Pickup(int packageId)
    {
        var studentId = await ResolveStudentId();
        var parcel = await _service.PickupAsync(packageId, studentId);
        return Ok(ApiResponse.Ok(parcel, "取件成功"));
    }
}
