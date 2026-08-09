using Microsoft.AspNetCore.Mvc;
using DormBackendFacilityNotice.Services;

namespace DormBackendFacilityNotice.Controllers;

/// <summary>
/// 水电分摊接口
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class FeeSharingController : ControllerBase
{
    private readonly IFeeSharingService _service;

    public FeeSharingController(IFeeSharingService service)
    {
        _service = service;
    }

    /// <summary>触发月度分摊（测试用）</summary>
    [HttpPost("calc-monthly")]
    public async Task<IActionResult> CalcMonthlyFee([FromQuery] string yearMonth = "2026-08")
    {
        await _service.CalcMonthlyFee(yearMonth);
        return Ok(new { message = $"月度分摊完成：{yearMonth}" });
    }

    /// <summary>触发退宿结算（测试用）</summary>
    [HttpPost("calc-checkout")]
    public async Task<IActionResult> CalcCheckoutFee(
        [FromQuery] string studentId,
        [FromQuery] int allocationId)
    {
        await _service.CalcCheckoutFee(studentId, allocationId);
        return Ok(new { message = $"退宿分摊完成：Student={studentId}, Allocation={allocationId}" });
    }

    /// <summary>查询个人分摊明细</summary>
    [HttpGet("detail")]
    public async Task<IActionResult> GetFeeDetail(
        [FromQuery] string studentId,
        [FromQuery] string yearMonth)
    {
        var result = await _service.GetFeeDetail(studentId, yearMonth);
        return Ok(new
        {
            studentId,
            yearMonth,
            count = result.Count,
            items = result.Select(f => new
            {
                f.DetailId,
                waterShare = f.WaterShare,
                powerShare = f.PowerShare,
                total = f.WaterShare + f.PowerShare,
                f.StayDays,
                f.TotalDays,
                f.BillType,
                f.IsPaid
            })
        });
    }
}
