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
    private readonly IBillingService _billingService;
    private readonly ILogger<InternalSchedulerController> _logger;
    private readonly IVisitorService _visitorService;

    public InternalSchedulerController(
        ICreditService creditService,
        IBillingService billingService,
        IVisitorService visitorService,
        ILogger<InternalSchedulerController> logger)
    {
        _creditService = creditService;
        _billingService = billingService;
        _visitorService = visitorService;
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

    /// <summary>
    /// IT-C3-003：手动触发当月首次自动扣款巡检。
    /// SP_Auto_Deduct 幂等：同月重复触发，已扣明细由幂等键与 Is_Paid 状态跳过，
    /// 不重复扣款；余额不足不扣并记录尝试。
    /// 每月 1/2/3 日的重试次数由 Quartz 作业内部管理，接口不接收参数。
    /// </summary>
    [HttpPost("deduction")]
    public async Task<IActionResult> Deduction()
    {
        await _billingService.AutoDeduct(1, DateTime.Now.ToString("yyyy-MM"));
        return Ok(ApiResponse.Ok(new { }, "自动扣款巡检完成"));
    }

    /// <summary>IT-C7-003：手动触发恢复供电巡检（SP_Restore_Power，已缴清房间复位供电）</summary>
    [HttpPost("power-restore")]
    public async Task<IActionResult> PowerRestore()
    {
        await _billingService.RestorePower();
        return Ok(ApiResponse.Ok(new { }, "恢复供电巡检完成"));
    }

    /// <summary>IT-C8-001：手动触发访客授权过期巡检（SVC-SCHED-06），将已到期有效授权置为「已过期」。</summary>
    [HttpPost("visitor-expire")]
    public async Task<IActionResult> VisitorExpire()
    {
        var expiredCount = await _visitorService.ExpireAsync();
        return Ok(ApiResponse.Ok(new { expiredCount }, $"访客过期处理完成，处理 {expiredCount} 条"));
    }

}
