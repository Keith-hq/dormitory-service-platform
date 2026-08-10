using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TemplateDormApi.Data;
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
    private readonly AppDbContext _context;

    public FacilityBookingController(IFacilityBookingService service, AppDbContext context)
    {
        _service = service;
        _context = context;
    }

    /// <summary>从 JWT 解析当前学生的 Student_ID</summary>
    private async Task<string> ResolveStudentId()
    {
        var accountId = CurrentUser.GetAccountId(User)
            ?? throw new UnauthorizedAccessException("未登录或 Token 无效");

        var studentId = await _context.UserAccounts
            .Where(a => a.AccountId == accountId)
            .Select(a => a.StudentId)
            .FirstOrDefaultAsync();

        return studentId ?? throw new UnauthorizedAccessException("当前账户未关联学生身份");
    }

    /// <summary>STU-20：预约设施</summary>
    [HttpPost("facility-bookings")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<object>>> Book([FromBody] BookRequest req)
    {
        var studentId = await ResolveStudentId();

        var (rc, bookingId) = await _service.BookFacility(req.FacilityId, studentId);

        var msgs = new[] { "预约成功", "设施不存在或不可用", "信用分不足（低于60）", "已有活跃预约", "设施已被占用" };
        return rc == 0
            ? Ok(ApiResponse.Ok(new { bookingId }, msgs[0]))
            : Ok(ApiResponse.Error(400, msgs[rc]));
    }

    /// <summary>STU-21：开始使用</summary>
    [HttpPost("facility-bookings/{bookingId}/start")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<object>>> Start(int bookingId)
    {
        var studentId = await ResolveStudentId();
        var rc = await _service.StartUse(bookingId, studentId);
        return rc == 0
            ? Ok(ApiResponse.Ok(new { }, "已开始使用"))
            : Ok(ApiResponse.Error(400, "预约不存在、状态不正确或身份不匹配"));
    }

    /// <summary>STU-22：结束使用</summary>
    [HttpPost("facility-bookings/{bookingId}/finish")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<object>>> Finish(int bookingId)
    {
        var studentId = await ResolveStudentId();
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
}
