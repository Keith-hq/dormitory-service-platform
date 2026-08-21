using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TemplateDormApi.DTO;
using TemplateDormApi.Security;
using TemplateDormApi.Services;

namespace TemplateDormApi.Controllers;

/// <summary>
/// 学生端通知中心接口。
/// </summary>
[ApiController]
[Authorize]
[Route("api/notifications")]
public class NotificationController : ControllerBase
{
    private readonly INotificationService _notificationService;

    public NotificationController(INotificationService notificationService)
    {
        _notificationService = notificationService;
    }

    [HttpGet]
    public async Task<IActionResult> GetPaged(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? isRead = null)
    {
        var accountId = CurrentUser.GetAccountId(User);
        if (!accountId.HasValue)
        {
            return Unauthorized(ApiResponse.Error(401, "用户身份无效"));
        }

        var result = await _notificationService.GetPagedAsync(accountId.Value, page, pageSize, isRead);
        return Ok(ApiResponse.Ok(result));
    }

    [HttpPut("{notificationId:int}/read")]
    public async Task<IActionResult> MarkRead(int notificationId)
    {
        var accountId = CurrentUser.GetAccountId(User);
        if (!accountId.HasValue)
        {
            return Unauthorized(ApiResponse.Error(401, "用户身份无效"));
        }

        await _notificationService.MarkReadAsync(notificationId, accountId.Value);
        return Ok(ApiResponse.Ok(new { }, "标记已读成功"));
    }

    [HttpPost("read-batch")]
    public async Task<IActionResult> MarkBatchRead([FromBody] ReadBatchDto dto)
    {
        var accountId = CurrentUser.GetAccountId(User);
        if (!accountId.HasValue)
        {
            return Unauthorized(ApiResponse.Error(401, "用户身份无效"));
        }

        await _notificationService.MarkBatchReadAsync(dto.Ids!, accountId.Value);
        return Ok(ApiResponse.Ok(new { }, "批量标记已读成功"));
    }

    [HttpGet("unread-count")]
    public async Task<IActionResult> GetUnreadCount()
    {
        var accountId = CurrentUser.GetAccountId(User);
        if (!accountId.HasValue)
        {
            return Unauthorized(ApiResponse.Error(401, "用户身份无效"));
        }

        var result = await _notificationService.GetUnreadCountAsync(accountId.Value);
        return Ok(ApiResponse.Ok(result));
    }
}
