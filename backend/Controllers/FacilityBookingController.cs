using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TemplateDormApi.Data;
using TemplateDormApi.DTO;
using TemplateDormApi.Exceptions;
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
    private readonly INotificationService _notificationService;
    private readonly ILogger<FacilityBookingController> _logger;

    public FacilityBookingController(
        IFacilityBookingService service,
        AppDbContext context,
        INotificationService notificationService,
        ILogger<FacilityBookingController> logger)
    {
        _service = service;
        _context = context;
        _notificationService = notificationService;
        _logger = logger;
    }

    /// <summary>从 JWT 解析当前学生的 Student_ID</summary>
    private async Task<string> ResolveStudentId()
    {
        var accountId = CurrentUser.GetAccountId(User)
            ?? throw new BusinessException(401, "未登录或 Token 无效");

        var studentId = await _context.UserAccounts
            .Where(a => a.AccountId == accountId)
            .Select(a => a.StudentId)
            .FirstOrDefaultAsync();

        return studentId ?? throw new BusinessException(401, "当前账户未关联学生身份");
    }

    /// <summary>STU-20：预约设施</summary>
    [HttpPost("facility-bookings")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<object>>> Book([FromBody] BookRequest req)
    {
        var studentId = await ResolveStudentId();

        var (rc, bookingId) = await _service.BookFacility(req.FacilityId, studentId);

        var msgs = new[] { "预约成功", "设施不存在或不可用", "信用分不足（低于60）", "已有活跃预约", "设施已被占用" };
        if (rc != 0)
        {
            return Ok(ApiResponse.Error(400, msgs[rc]));
        }

        await TryNotifyBookingAsync(studentId, req.FacilityId, bookingId);
        return Ok(ApiResponse.Ok(new { bookingId }, msgs[0]));
    }

    /// <summary>
    /// 预约成功后的通知（fail-soft，不阻断预约主流程）：
    /// 通知学生本人 + 设施所在楼栋宿管。
    /// </summary>
    private async Task TryNotifyBookingAsync(string studentId, int facilityId, int bookingId)
    {
        try
        {
            await _notificationService.CreateAsync(new NotificationCreateDto
            {
                StudentId = studentId,
                Title = "设施预约成功",
                Content = $"您已成功预约设施（ID {facilityId}），预约单号 {bookingId}，请按时使用。",
                NotificationType = "预约"
            });

            var facility = await _context.Facilities.AsNoTracking()
                .FirstOrDefaultAsync(item => item.FacilityId == facilityId);
            if (facility is not null)
            {
                var adminId = await _context.Admins.AsNoTracking()
                    .Where(admin => admin.BuildingId == facility.BuildingId)
                    .OrderBy(admin => admin.AdminId)
                    .Select(admin => admin.AdminId)
                    .FirstOrDefaultAsync();
                if (!string.IsNullOrWhiteSpace(adminId))
                {
                    await _notificationService.CreateAsync(new NotificationCreateDto
                    {
                        AdminId = adminId,
                        Title = "新增设施预约",
                        Content = $"学生 {studentId} 预约了设施（ID {facilityId}，预约单 {bookingId}）。",
                        NotificationType = "预约"
                    });
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "设施预约通知投递失败，studentId={StudentId}, bookingId={BookingId}", studentId, bookingId);
        }
    }

    /// <summary>STU-20 配套：查询我的预约记录（含状态，供前端展示与释放操作）。</summary>
    [HttpGet("facility-bookings/my")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<object>>> MyBookings(CancellationToken cancellationToken)
    {
        var studentId = await ResolveStudentId();
        var bookings = await _service.GetMyBookingsAsync(studentId, cancellationToken);
        return Ok(ApiResponse.Ok(bookings));
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
