using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TemplateDormApi.DTO;
using TemplateDormApi.Security;
using TemplateDormApi.Services;

namespace TemplateDormApi.Controllers;

/// <summary>
/// 公共设施预约接口
/// </summary>
[ApiController]
[Route("api")]
public class FacilityBookingController : ControllerBase
{
    private readonly IFacilityBookingService _service;

    public FacilityBookingController(IFacilityBookingService service)
    {
        _service = service;
    }

    /// <summary>STU-20：预约设施</summary>
    [HttpPost("facility-bookings")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<object>>> Book([FromBody] BookRequest req)
    {
        var accountId = CurrentUser.GetAccountId(User);
        if (accountId == null) return Unauthorized();

        // 当前 JWT 使用 Account_ID，存储过程使用 Student_ID
        // 在 UserAccount ↔ Student 映射未实现前，通过请求体传入 studentId
        var (rc, bookingId) = await _service.BookFacility(req.FacilityId, req.StudentId);

        var msgs = new[] { "预约成功", "设施不存在或不可用", "信用分不足（低于60）", "已有活跃预约", "设施已被占用" };
        return rc == 0
            ? Ok(ApiResponse.Ok(new { bookingId }, msgs[0]))
            : Ok(ApiResponse.Error(400 + rc, msgs[rc]));
    }

    /// <summary>STU-21：开始使用</summary>
    [HttpPost("facility-bookings/{bookingId}/start")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<object>>> Start(int bookingId, [FromQuery] string studentId)
    {
        var rc = await _service.StartUse(bookingId, studentId);
        return rc == 0
            ? Ok(ApiResponse.Ok(new { }, "已开始使用"))
            : Ok(ApiResponse.Error(400, "预约不存在、状态不正确或身份不匹配"));
    }

    /// <summary>STU-22：结束使用</summary>
    [HttpPost("facility-bookings/{bookingId}/finish")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<object>>> Finish(int bookingId, [FromQuery] string studentId)
    {
        var rc = await _service.FinishUse(bookingId, studentId);
        return rc == 0
            ? Ok(ApiResponse.Ok(new { }, "已结束使用"))
            : Ok(ApiResponse.Error(400, "预约不存在、状态不正确或身份不匹配"));
    }

    /// <summary>过期巡检（Internal + ServiceKeyAuth）</summary>
    [HttpPost("internal/scheduler/booking-expire")]
    [ServiceKeyAuth]
    public async Task<ActionResult<ApiResponse<object>>> Expire()
    {
        await _service.ExpireBookings();
        return Ok(ApiResponse.Ok(new { }, "过期巡检完成"));
    }

    /// <summary>超时完成巡检（Internal + ServiceKeyAuth）</summary>
    [HttpPost("internal/scheduler/booking-auto-complete")]
    [ServiceKeyAuth]
    public async Task<ActionResult<ApiResponse<object>>> AutoComplete()
    {
        await _service.AutoComplete();
        return Ok(ApiResponse.Ok(new { }, "超时完成巡检完成"));
    }
}

public class BookRequest
{
    public int FacilityId { get; set; }
    public string StudentId { get; set; } = string.Empty;
}
