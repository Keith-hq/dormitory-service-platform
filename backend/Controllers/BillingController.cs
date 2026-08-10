using Microsoft.AspNetCore.Mvc;
using TemplateDormApi.Services;

namespace TemplateDormApi.Controllers;

/// <summary>
/// 账单划扣与断电接口（测试用）
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class BillingController : ControllerBase
{
    private readonly IBillingService _service;

    public BillingController(IBillingService service)
    {
        _service = service;
    }

    /// <summary>触发自动扣款（测试用）</summary>
    [HttpPost("auto-deduct")]
    public async Task<IActionResult> AutoDeduct(
        [FromQuery] int attemptNo = 1,
        [FromQuery] string yearMonth = "2026-08")
    {
        await _service.AutoDeduct(attemptNo, yearMonth);
        return Ok(new { message = $"第{attemptNo}次扣款完成：{yearMonth}" });
    }

    /// <summary>触发断电判定（测试用）</summary>
    [HttpPost("check-power-cut")]
    public async Task<IActionResult> CheckPowerCut(
        [FromQuery] string yearMonth = "2026-08")
    {
        await _service.CheckPowerCut(yearMonth);
        return Ok(new { message = $"断电判定完成：{yearMonth}" });
    }

    /// <summary>触发恢复供电（测试用）</summary>
    [HttpPost("restore-power")]
    public async Task<IActionResult> RestorePower()
    {
        await _service.RestorePower();
        return Ok(new { message = "恢复供电巡检完成" });
    }

    /// <summary>查询余额</summary>
    [HttpGet("balance")]
    public async Task<IActionResult> GetBalance([FromQuery] string studentId)
    {
        var balance = await _service.GetBalance(studentId);
        return Ok(new { studentId, balance });
    }

    /// <summary>查询供电状态</summary>
    [HttpGet("power-status")]
    public async Task<IActionResult> GetPowerStatus([FromQuery] int roomId)
    {
        var status = await _service.GetPowerStatus(roomId);
        return Ok(new { roomId, powerStatus = status });
    }
}
