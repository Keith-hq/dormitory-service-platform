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
        var result = await _service.GetStudentEntriesAsync(studentId, query, cancellationToken);
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
        var result = await _service.UpdateReasonAsync(recordId, request, cancellationToken);
        return Ok(ApiResponse.Ok(result, "晚归说明更新成功"));
    }

    /// <summary>DORM-31 人工登记晚归。</summary>
    [Authorize(Policy = AuthPolicies.DormAdmin)]
    [HttpPost("late-entries")]
    public async Task<ActionResult<ApiResponse<LateEntryDto>>> Create(
        [FromBody] CreateLateEntryRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _service.CreateAsync(request, cancellationToken);
        return Ok(ApiResponse.Ok(result, "晚归登记成功"));
    }
}
