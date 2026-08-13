using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TemplateDormApi.DTO;
using TemplateDormApi.Security;
using TemplateDormApi.Services;

namespace TemplateDormApi.Controllers;

[ApiController]
[Route("api")]
public sealed class HygieneController : ControllerBase
{
    private readonly IHygieneService _service;

    public HygieneController(IHygieneService service)
    {
        _service = service;
    }

    /// <summary>STU-18 查询房间卫生成绩。</summary>
    [Authorize]
    [HttpGet("rooms/{roomId:long}/hygiene")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<HygieneRecordDto>>>> GetRoomRecords(
        long roomId,
        CancellationToken cancellationToken)
    {
        var isDormAdmin = User.IsInRole("admin") || User.IsInRole("super_admin");
        var accountId = CurrentUser.GetAccountId(User);
        if (!isDormAdmin && !accountId.HasValue)
        {
            return Unauthorized(ApiResponse.Error(401, "用户身份无效"));
        }

        var result = await _service.GetRoomRecordsAsync(
            roomId,
            accountId,
            isDormAdmin,
            cancellationToken);
        return Ok(ApiResponse.Ok(result));
    }

    /// <summary>DORM-32 卫生打分。</summary>
    [Authorize]
    [HttpPost("hygiene-records")]
    public async Task<ActionResult<ApiResponse<HygieneRecordDto>>> Create(
        [FromBody] CreateHygieneRecordRequest request,
        CancellationToken cancellationToken)
    {
        var accountId = CurrentUser.GetAccountId(User);
        if (!accountId.HasValue)
        {
            return Unauthorized(ApiResponse.Error(401, "用户身份无效"));
        }

        var result = await _service.CreateAsync(accountId.Value, request, cancellationToken);
        return Ok(ApiResponse.Ok(result, "卫生评分登记成功"));
    }

    /// <summary>DORM-33 修改卫生评分。</summary>
    [Authorize]
    [HttpPut("hygiene-records/{recordId:long}")]
    public async Task<ActionResult<ApiResponse<HygieneRecordDto>>> Update(
        long recordId,
        [FromBody] UpdateHygieneRecordRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _service.UpdateAsync(recordId, request, cancellationToken);
        return Ok(ApiResponse.Ok(result, "卫生评分修改成功"));
    }

    /// <summary>DORM-34 查询卫生月度排名。</summary>
    [Authorize]
    [HttpGet("hygiene-rankings")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<HygieneRankingDto>>>> GetRankings(
        [FromQuery] HygieneRankingQueryDto query,
        CancellationToken cancellationToken)
    {
        var isDormAdmin = User.IsInRole("admin") || User.IsInRole("super_admin");
        var accountId = CurrentUser.GetAccountId(User);
        if (!accountId.HasValue)
        {
            return Unauthorized(ApiResponse.Error(401, "用户身份无效"));
        }

        var result = await _service.GetRankingsAsync(query, accountId.Value, isDormAdmin, cancellationToken);
        return Ok(ApiResponse.Ok(result));
    }
}
