using Microsoft.AspNetCore.Mvc;
using TemplateDormApi.DTO;
using TemplateDormApi.Security;
using TemplateDormApi.Services;

namespace TemplateDormApi.Controllers;

/// <summary>
/// 供内部服务调用的信用分接口。
/// </summary>
[ApiController]
[Route("api/internal/credit")]
[ServiceKeyAuth]
public class InternalCreditController : ControllerBase
{
    private readonly ICreditService _creditService;

    public InternalCreditController(ICreditService creditService)
    {
        _creditService = creditService;
    }

    [HttpPost("deduct")]
    public async Task<IActionResult> Deduct(
        [FromBody] CreditDeductDto dto,
        CancellationToken cancellationToken)
    {
        var result = await _creditService.DeductAsync(dto, cancellationToken);
        return Ok(ApiResponse.Ok(result));
    }

    [HttpGet("status/{studentId}")]
    public async Task<IActionResult> GetStatus(
        string studentId,
        CancellationToken cancellationToken)
    {
        var result = await _creditService.GetStatusAsync(studentId, cancellationToken);
        return Ok(ApiResponse.Ok(result));
    }
}
