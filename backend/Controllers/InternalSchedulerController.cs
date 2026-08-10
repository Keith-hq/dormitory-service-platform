using Microsoft.AspNetCore.Mvc;
using TemplateDormApi.DTO;
using TemplateDormApi.Security;
using TemplateDormApi.Services;

namespace TemplateDormApi.Controllers;

/// <summary>
/// 供内部服务调用的定时任务接口。
/// </summary>
[ApiController]
[Route("api/internal/scheduler")]
[ServiceKeyAuth]
public class InternalSchedulerController : ControllerBase
{
    private readonly ICreditService _creditService;
    private readonly ILogger<InternalSchedulerController> _logger;

    public InternalSchedulerController(
        ICreditService creditService,
        ILogger<InternalSchedulerController> logger)
    {
        _creditService = creditService;
        _logger = logger;
    }

    [HttpPost("credit-reset")]
    public async Task<IActionResult> CreditReset(CancellationToken cancellationToken)
    {
        var now = DateTime.Now;
        var result = await _creditService.ResetMonthlyAsync(
            now.Year,
            now.Month,
            cancellationToken);

        _logger.LogInformation(
            "手动触发信用分月度重置：{Year}-{Month}，Processed={Processed}，Skipped={Skipped}，Failed={Failed}",
            now.Year,
            now.Month,
            result.Processed,
            result.Skipped,
            result.Failed);

        return Ok(ApiResponse.Ok(result));
    }
}
