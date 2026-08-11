using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TemplateDormApi.DTO;
using TemplateDormApi.Security;
using TemplateDormApi.Services;

namespace TemplateDormApi.Controllers;

[ApiController]
[Authorize(Policy = AuthPolicies.DormAdmin)]
[Route("api/violations")]
public sealed class ViolationController : ControllerBase
{
    private readonly IViolationService _service;

    public ViolationController(IViolationService service)
    {
        _service = service;
    }

    /// <summary>VIOL-01 登记违规违纪。</summary>
    [HttpPost]
    public async Task<ActionResult<ApiResponse<ViolationDto>>> Create(
        [FromBody] CreateViolationRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _service.CreateAsync(request, cancellationToken);
        return Ok(ApiResponse.Ok(result, "违规记录登记成功"));
    }

    /// <summary>VIOL-02 查询违规记录。</summary>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<ViolationDto>>>> GetPaged(
        [FromQuery] ViolationQueryDto query,
        CancellationToken cancellationToken)
    {
        var result = await _service.GetPagedAsync(query, cancellationToken);
        return Ok(ApiResponse.Ok(result));
    }
}
