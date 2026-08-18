using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TemplateDormApi.DTO;
using TemplateDormApi.Security;
using TemplateDormApi.Services;

namespace TemplateDormApi.Controllers;

/// <summary>
/// 信用分申诉接口（APPEAL-01/02/03）：学生提交/查询本人申诉，楼长/超管复核。
/// </summary>
[ApiController]
[Authorize]
[Route("api")]
public class CreditAppealController : ControllerBase
{
    private readonly ICreditAppealService _service;

    public CreditAppealController(ICreditAppealService service)
    {
        _service = service;
    }

    /// <summary>APPEAL-01 提交申诉 — POST /credit-appeals</summary>
    [HttpPost("credit-appeals")]
    public async Task<IActionResult> Submit(
        [FromBody] CreateCreditAppealRequest dto,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ApiResponse.Error(400, "参数校验失败"));
        }

        var accountId = CurrentUser.GetAccountId(User);
        if (!accountId.HasValue)
        {
            return Unauthorized(ApiResponse.Error(401, "用户身份无效"));
        }

        var result = await _service.SubmitAsync(accountId.Value, dto, cancellationToken);
        return Ok(ApiResponse.Created(result));
    }

    /// <summary>APPEAL-02 我的申诉 — GET /students/{studentId}/credit-appeals（本人或宿管）</summary>
    [HttpGet("students/{studentId}/credit-appeals")]
    public async Task<IActionResult> MyList(
        string studentId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        var accountId = CurrentUser.GetAccountId(User);
        if (!accountId.HasValue)
        {
            return Unauthorized(ApiResponse.Error(401, "用户身份无效"));
        }

        // 学生本人可查自己的申诉；宿管/超管（DormAdmin 角色）显式放行。
        var isDormAdmin = User.IsInRole(AuthPolicies.Admin) || User.IsInRole(AuthPolicies.SuperAdmin);

        var result = await _service.GetMyAsync(
            accountId.Value,
            studentId,
            page,
            pageSize,
            isDormAdmin,
            cancellationToken);
        return Ok(ApiResponse.Ok(result));
    }

    /// <summary>APPEAL-03 复核申诉 — PUT /credit-appeals/{appealId}/review（楼长/超管）</summary>
    [HttpPut("credit-appeals/{appealId}/review")]
    [Authorize(Policy = AuthPolicies.DormAdmin)]
    public async Task<IActionResult> Review(
        int appealId,
        [FromBody] ReviewCreditAppealRequest dto,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ApiResponse.Error(400, "参数校验失败"));
        }

        var accountId = CurrentUser.GetAccountId(User);
        if (!accountId.HasValue)
        {
            return Unauthorized(ApiResponse.Error(401, "用户身份无效"));
        }

        var result = await _service.ReviewAsync(
            appealId,
            accountId.Value,
            dto,
            cancellationToken);
        return Ok(ApiResponse.Ok(result));
    }
}
