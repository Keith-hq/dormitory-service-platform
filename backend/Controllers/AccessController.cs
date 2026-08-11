using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TemplateDormApi.DTO;
using TemplateDormApi.Security;
using TemplateDormApi.Services;

namespace TemplateDormApi.Controllers;

[ApiController]
[Authorize(Policy = AuthPolicies.DormAdmin)]
[Route("api/access-logs")]
public sealed class AccessController : ControllerBase
{
    private readonly IAccessService _service;

    public AccessController(IAccessService service)
    {
        _service = service;
    }

    /// <summary>ACCESS-01 查询门禁记录。</summary>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<AccessLogDto>>>> GetLogs(
        [FromQuery] AccessLogQueryDto query,
        CancellationToken cancellationToken)
    {
        var result = await _service.GetLogsAsync(query, cancellationToken);
        return Ok(ApiResponse.Ok(result));
    }

    /// <summary>ACCESS-02 查询楼内实时密度。</summary>
    [HttpGet("density")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<AccessDensityDto>>>> GetDensity(
        [FromQuery] AccessDensityQueryDto query,
        CancellationToken cancellationToken)
    {
        var result = await _service.GetDensityAsync(query, cancellationToken);
        return Ok(ApiResponse.Ok(result));
    }
}
