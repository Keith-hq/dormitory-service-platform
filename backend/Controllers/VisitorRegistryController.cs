using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TemplateDormApi.DTO;
using TemplateDormApi.Security;
using TemplateDormApi.Services;

namespace TemplateDormApi.Controllers;

[ApiController]
[Authorize(Policy = AuthPolicies.DormAdmin)]
[Route("api/visitor-registry")]
public sealed class VisitorRegistryController : ControllerBase
{
    private readonly IVisitorRegistryService _service;

    public VisitorRegistryController(IVisitorRegistryService service)
    {
        _service = service;
    }

    /// <summary>VST-01 门岗登记访客。</summary>
    [HttpPost]
    public async Task<ActionResult<ApiResponse<VisitorRegistryDto>>> Create(
        [FromBody] CreateVisitorRegistryRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _service.CreateAsync(request, cancellationToken);
        return Ok(ApiResponse.Ok(result, "访客登记成功"));
    }

    /// <summary>在场(尚未离场)访客列表，供门岗逐个办理离场登记。</summary>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<VisitorRegistryDto>>>> GetActive(
        CancellationToken cancellationToken)
    {
        var result = await _service.GetActiveAsync(cancellationToken);
        return Ok(ApiResponse.Ok(result));
    }

    /// <summary>VST-02 扫码核验。</summary>
    [HttpPost("{registryId:long}/verify")]
    public async Task<ActionResult<ApiResponse<VisitorRegistryDto>>> Verify(
        long registryId,
        [FromBody] VerifyVisitorRegistryRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _service.VerifyAsync(registryId, request, cancellationToken);
        return Ok(ApiResponse.Ok(result, "访客核验成功"));
    }

    /// <summary>VST-03 记录访客离开。</summary>
    [HttpPost("{registryId:long}/exit")]
    public async Task<ActionResult<ApiResponse<VisitorRegistryDto>>> RecordExit(
        long registryId,
        CancellationToken cancellationToken)
    {
        var result = await _service.RecordExitAsync(registryId, cancellationToken);
        return Ok(ApiResponse.Ok(result, "访客离开记录成功"));
    }
}
