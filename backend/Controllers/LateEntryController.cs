using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TemplateDormApi.DTO;
using TemplateDormApi.Security;
using TemplateDormApi.Services;

namespace TemplateDormApi.Controllers;

[ApiController]
[Route("api")]
public sealed class LateEntryController : ControllerBase
{
    private readonly ILateEntryService _service;

    public LateEntryController(ILateEntryService service)
    {
        _service = service;
    }

    /// <summary>STU-13 查询我的晚归记录。</summary>
    [Authorize]
    [HttpGet("students/{studentId}/late-entries")]
    public async Task<ActionResult<ApiResponse<PagedResult<LateEntryDto>>>> GetStudentEntries(
        string studentId,
        [FromQuery] LateEntryQueryDto query,
        CancellationToken cancellationToken)
    {
        var accountId = CurrentUser.GetAccountId(User);
        if (!accountId.HasValue)
        {
            return Unauthorized(ApiResponse.Error(401, "用户身份无效"));
        }

        var result = await _service.GetStudentEntriesAsync(studentId, accountId.Value, query, cancellationToken);
        return Ok(ApiResponse.Ok(result));
    }

    /// <summary>STU-14 补充晚归说明。</summary>
    [Authorize]
    [HttpPut("late-entries/{recordId:long}/reason")]
    public async Task<ActionResult<ApiResponse<LateEntryDto>>> UpdateReason(
        long recordId,
        [FromBody] UpdateLateEntryReasonRequest request,
        CancellationToken cancellationToken)
    {
        var accountId = CurrentUser.GetAccountId(User);
        if (!accountId.HasValue)
        {
            return Unauthorized(ApiResponse.Error(401, "用户身份无效"));
        }

        var result = await _service.UpdateReasonAsync(recordId, accountId.Value, request, cancellationToken);
        return Ok(ApiResponse.Ok(result, "晚归说明更新成功"));
    }

    /// <summary>DORM-31 人工登记晚归。</summary>
    [Authorize]
    [HttpPost("late-entries")]
    public async Task<ActionResult<ApiResponse<LateEntryDto>>> Create(
        [FromBody] CreateLateEntryRequest request,
        CancellationToken cancellationToken)
    {
        var accountId = CurrentUser.GetAccountId(User);
        if (!accountId.HasValue)
        {
            return Unauthorized(ApiResponse.Error(401, "用户身份无效"));
        }

        var result = await _service.CreateAsync(accountId.Value, request, cancellationToken);
        return Ok(ApiResponse.Ok(result, "晚归登记成功"));
    }
}
