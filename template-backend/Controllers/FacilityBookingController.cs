using Microsoft.AspNetCore.Mvc;
using TemplateDormApi.Services;

namespace TemplateDormApi.Controllers;

/// <summary>
/// 公共设施预约接口
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class FacilityBookingController : ControllerBase
{
    private readonly IFacilityBookingService _service;

    public FacilityBookingController(IFacilityBookingService service)
    {
        _service = service;
    }

    /// <summary>预约设施</summary>
    [HttpPost("book")]
    public async Task<IActionResult> Book([FromQuery] int facilityId, [FromQuery] string studentId)
    {
        var rc = await _service.BookFacility(facilityId, studentId);
        var msgs = new[] { "预约成功", "设施不可用", "信用分不足", "已有活跃预约" };
        return Ok(new { resultCode = rc, message = msgs[rc >= 0 && rc < msgs.Length ? rc : 0] });
    }

    /// <summary>开始使用</summary>
    [HttpPost("start")]
    public async Task<IActionResult> Start([FromQuery] int bookingId, [FromQuery] string studentId)
    {
        var rc = await _service.StartUse(bookingId, studentId);
        return Ok(new { resultCode = rc, message = rc == 0 ? "已开始" : rc == 1 ? "状态错误" : "身份不匹配" });
    }

    /// <summary>结束使用</summary>
    [HttpPost("end")]
    public async Task<IActionResult> End([FromQuery] int bookingId, [FromQuery] string studentId)
    {
        var rc = await _service.EndUse(bookingId, studentId);
        return Ok(new { resultCode = rc, message = rc == 0 ? "已结束" : rc == 1 ? "状态错误" : "身份不匹配" });
    }

    /// <summary>过期巡检（测试用）</summary>
    [HttpPost("expire")]
    public async Task<IActionResult> Expire()
    {
        await _service.ExpireBookings();
        return Ok(new { message = "过期巡检完成" });
    }

    /// <summary>超时完成巡检（测试用）</summary>
    [HttpPost("auto-complete")]
    public async Task<IActionResult> AutoComplete()
    {
        await _service.AutoComplete();
        return Ok(new { message = "超时完成巡检完成" });
    }
}
