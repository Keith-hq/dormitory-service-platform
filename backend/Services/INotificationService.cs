using TemplateDormApi.DTO;

namespace TemplateDormApi.Services;

/// <summary>
/// 通知中心业务服务。
/// </summary>
public interface INotificationService
{
    Task<PagedResult<NotificationItemDto>> GetPagedAsync(
        int recipientAccountId,
        int page,
        int pageSize,
        string? isRead);

    Task MarkReadAsync(int notificationId, int recipientAccountId);

    Task MarkBatchReadAsync(IReadOnlyCollection<int> notificationIds, int recipientAccountId);

    Task<UnreadCountDto> GetUnreadCountAsync(int recipientAccountId);

    Task<NotificationItemDto> CreateAsync(NotificationCreateDto dto);
}
