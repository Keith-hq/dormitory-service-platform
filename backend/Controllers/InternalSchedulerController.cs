using System.Text.RegularExpressions;
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
    private static readonly Regex YearMonthRegex = new(@"^\d{4}-(0[1-9]|1[0-2])$", RegexOptions.Compiled);

    private readonly ICreditService _creditService;
    private readonly IBillingService _billingService;
    private readonly ILogger<InternalSchedulerController> _logger;

    public InternalSchedulerController(
        ICreditService creditService,
        IBillingService billingService,
        ILogger<InternalSchedulerController> logger)
    {
        _creditService = creditService;
        _billingService = billingService;
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
    /// IT-C3-003：手动触发自动扣款（每月1/2/3日定时任务的同款入口）。
    /// SP_Auto_Deduct 幂等：同月重复触发，已扣明细由幂等键与 Is_Paid 状态跳过，
    /// 不重复扣款；余额不足不扣并记录尝试。
    /// 参数偏差（PR #58 P2 登记）：契约写"无需参数"，实现为可选 attemptNo/yearMonth
    /// （attemptNo 用于 IT-C3-002 三次尝试口径，默认 1；yearMonth 默认当月）。
    /// 内部接口可接受，已登记待同步契约（C-025 同类）。
    /// </summary>
    [HttpPost("deduction")]
    public async Task<IActionResult> Deduction(
        [FromQuery] int attemptNo = 1,
        [FromQuery] string? yearMonth = null)
    {
        if (attemptNo < 1 || attemptNo > 3)
            return BadRequest(ApiResponse.Error(400, "attemptNo 必须在 1~3 之间"));

        var targetMonth = string.IsNullOrWhiteSpace(yearMonth)
            ? DateTime.Now.ToString("yyyy-MM")
            : yearMonth;

        if (!TryValidateYearMonth(targetMonth, out var message))
            return BadRequest(ApiResponse.Error(400, message));

        await _billingService.AutoDeduct(attemptNo, targetMonth);
        return Ok(ApiResponse.Ok(new { attemptNo, yearMonth = targetMonth }, $"自动扣款完成：第{attemptNo}次 {targetMonth}"));
    }

    /// <summary>IT-C7-003：手动触发恢复供电巡检（SP_Restore_Power，已缴清房间复位供电）</summary>
    [HttpPost("power-restore")]
    public async Task<IActionResult> PowerRestore()
    {
        await _billingService.RestorePower();
        return Ok(ApiResponse.Ok(new { }, "恢复供电巡检完成"));
    }

    /// <summary>校验 yyyy-MM 格式及账期范围（不早于 2020-01，不晚于下月）</summary>
    private static bool TryValidateYearMonth(string yearMonth, out string message)
    {
        if (!YearMonthRegex.IsMatch(yearMonth))
        {
            message = "yearMonth 格式必须为 yyyy-MM";
            return false;
        }

        var now = DateTime.Now;
        var currentMonth = new DateTime(now.Year, now.Month, 1);
        var minMonth = new DateTime(2020, 1, 1);
        var target = new DateTime(int.Parse(yearMonth[..4]), int.Parse(yearMonth[5..7]), 1);

        if (target < minMonth || target > currentMonth.AddMonths(1))
        {
            message = $"yearMonth 必须在 {minMonth:yyyy-MM} 至 {currentMonth.AddMonths(1):yyyy-MM} 之间";
            return false;
        }

        message = string.Empty;
        return true;
    }
}
