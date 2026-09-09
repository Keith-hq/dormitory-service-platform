using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Linq;
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
    private static readonly string[] AllowedTimeSlots = new[]
        { "08:00", "10:00", "14:00", "16:00", "19:00", "21:00" };

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

        // 可选：预约某(日期, 时段)。给出时写入所选时段(Start/End)，未给则维持即时占用(无时段)。
        DateTime? slotStart = null;
        DateTime? slotEnd = null;
        var hasSlot = !string.IsNullOrWhiteSpace(req.Date) && !string.IsNullOrWhiteSpace(req.TimeSlot);
        if (hasSlot)
        {
            if (!DateTime.TryParseExact(
                    $"{req.Date} {req.TimeSlot}",
                    "yyyy-MM-dd HH:mm",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out var parsed))
            {
                return Ok(ApiResponse.Error(400, "日期或时段格式不正确（应为 2026-09-11 与 10:00）"));
            }
            if (parsed.Date < DateTime.Today)
            {
                return Ok(ApiResponse.Error(400, "不能预约过去的日期"));
            }
            if (!AllowedTimeSlots.Contains(req.TimeSlot!))
            {
                return Ok(ApiResponse.Error(400, "时段不合法，可选 08:00/10:00/14:00/16:00/19:00/21:00"));
            }
            slotStart = parsed;
            slotEnd = parsed.AddHours(2);
        }

        var (rc, bookingId) = await _service.BookFacility(req.FacilityId, studentId, slotStart, slotEnd);

        var msgs = new[]
        {
            "预约成功", "设施不存在或不可用", "信用分不足（低于60）", "已有活跃预约",
            hasSlot ? "该时段已被预约" : "设施已被占用"
        };
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

    /// <summary>按(日期)返回各设施已被预约/使用的时段，供前端列表按真实预约展示空闲。</summary>
    [HttpGet("facility-bookings/availability")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<object>>> Availability(
        [FromQuery] string? date,
        CancellationToken cancellationToken)
    {
        var target = DateTime.TryParseExact(
            string.IsNullOrWhiteSpace(date) ? DateTime.Today.ToString("yyyy-MM-dd") : date!,
            "yyyy-MM-dd",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out var parsed)
            ? parsed
            : DateTime.Today;

        var start = target;
        var end = target.AddDays(1);
        var occupiedHours = await _context.FacilityBookings
            .AsNoTracking()
            .Where(b =>
                (b.Status == "已预约" || b.Status == "使用中") &&
                b.StartTime.HasValue &&
                b.StartTime >= start &&
                b.StartTime < end)
            .Select(b => new { b.FacilityId, Hour = b.StartTime!.Value.Hour })
            .ToListAsync(cancellationToken);

        var byFacility = occupiedHours
            .GroupBy(o => o.FacilityId)
            .ToDictionary(g => g.Key, g => g.Select(o => o.Hour).ToHashSet());

        var slotHours = new[] { 8, 10, 14, 16, 19, 21 };
        var facilities = await _context.Facilities.AsNoTracking()
            .Select(f => f.FacilityId)
            .ToListAsync(cancellationToken);

        var items = facilities.Select(facilityId => new
        {
            facilityId,
            occupiedSlots = slotHours
                .Where(hour => byFacility.TryGetValue(facilityId, out var hours) && hours.Contains(hour))
                .Select(hour => $"{hour:00}:00")
                .ToList()
        }).ToList();

        return Ok(ApiResponse.Ok(new { date = target.ToString("yyyy-MM-dd"), items }));
    }
}

public class BookRequest
{
    public int FacilityId { get; set; }

    /// <summary>预约日期，如 2026-09-11；与 TimeSlot 一起提供时按(日期,时段)占用</summary>
    public string? Date { get; set; }

    /// <summary>预约时段起点，如 10:00；每段 2 小时</summary>
    public string? TimeSlot { get; set; }
}
