using Microsoft.AspNetCore.Mvc;
using TemplateDormApi.DTO;
using TemplateDormApi.Security;
using TemplateDormApi.Services;

namespace TemplateDormApi.Controllers;

/// <summary>
/// 供内部服务调用的通知投递接口。
/// </summary>
[ApiController]
[Route("api/internal/notifications")]
[ServiceKeyAuth]
public class InternalNotificationController : ControllerBase
{
    private readonly INotificationService _notificationService;

    public InternalNotificationController(INotificationService notificationService)
    {
        _notificationService = notificationService;
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] NotificationCreateDto dto)
    {
        var notification = await _notificationService.CreateAsync(dto);
        return Ok(ApiResponse.Ok(notification, "投递成功"));
    }
}
