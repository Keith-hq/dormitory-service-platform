using Microsoft.AspNetCore.Http;
using TemplateDormApi.DTO;
using TemplateDormApi.Exceptions;
using TemplateDormApi.Models;
using TemplateDormApi.Repository;

namespace TemplateDormApi.Services;

/// <summary>
/// 通知中心业务逻辑。
/// </summary>
public class NotificationService : INotificationService
{
    private const int MaxPageSize = 100;
    private readonly NotificationRepository _notificationRepository;
    private readonly UserAccountRepository _userAccountRepository;

    public NotificationService(
        NotificationRepository notificationRepository,
        UserAccountRepository userAccountRepository)
    {
        _notificationRepository = notificationRepository;
        _userAccountRepository = userAccountRepository;
    }

    public async Task<PagedResult<NotificationItemDto>> GetPagedAsync(
        int recipientAccountId,
        int page,
        int pageSize,
        string? isRead)
    {
        ValidatePaging(page, pageSize);
        var readStatus = ParseReadStatus(isRead);
        var (items, total) = await _notificationRepository.GetPagedAsync(
            recipientAccountId,
            page,
            pageSize,
            readStatus);

        return new PagedResult<NotificationItemDto>
        {
            Items = items.Select(ToItemDto).ToList(),
            Total = total,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task MarkReadAsync(int notificationId, int recipientAccountId)
    {
        if (!await _notificationRepository.ExistsAsync(notificationId, recipientAccountId))
        {
            throw new BusinessException(404, "通知不存在", StatusCodes.Status404NotFound);
        }

        await _notificationRepository.MarkReadAsync(notificationId, recipientAccountId);
    }

    public Task MarkBatchReadAsync(IReadOnlyCollection<int> notificationIds, int recipientAccountId)
    {
        return _notificationRepository.MarkBatchReadAsync(notificationIds, recipientAccountId);
    }

    public async Task<UnreadCountDto> GetUnreadCountAsync(int recipientAccountId)
    {
        var count = await _notificationRepository.CountUnreadAsync(recipientAccountId);
        return new UnreadCountDto { Count = count };
    }

    public async Task<NotificationItemDto> CreateAsync(NotificationCreateDto dto)
    {
        var hasStudentId = !string.IsNullOrWhiteSpace(dto.StudentId);
        var hasAdminId = !string.IsNullOrWhiteSpace(dto.AdminId);

        if (hasStudentId == hasAdminId)
        {
            throw new BusinessException(400, "studentId 与 adminId 必须且只能提供一个");
        }

        int? recipientAccountId = hasStudentId
            ? await _userAccountRepository.GetByStudentIdAsync(dto.StudentId!)
            : await _userAccountRepository.GetByAdminIdAsync(dto.AdminId!);

        if (!recipientAccountId.HasValue)
        {
            throw new BusinessException(40401, "接收账户不存在", StatusCodes.Status404NotFound);
        }

        var notification = new Notification
        {
            RecipientAccountId = recipientAccountId.Value,
            Title = dto.Title,
            Content = dto.Content,
            NotificationType = dto.NotificationType
        };

        var created = await _notificationRepository.AddAsync(notification);
        return ToItemDto(created);
    }

    private static void ValidatePaging(int page, int pageSize)
    {
        if (page < 1)
        {
            throw new BusinessException(400, "page 必须大于等于 1");
        }

        if (pageSize < 1 || pageSize > MaxPageSize)
        {
            throw new BusinessException(400, $"pageSize 必须在 1 到 {MaxPageSize} 之间");
        }

        if ((long)(page - 1) * pageSize > int.MaxValue)
        {
            throw new BusinessException(400, "page 与 pageSize 组合超出支持范围");
        }
    }

    private static bool? ParseReadStatus(string? isRead)
    {
        if (isRead is null)
        {
            return null;
        }

        return isRead switch
        {
            "已读" => true,
            "未读" => false,
            _ => throw new BusinessException(400, "isRead 只能为已读或未读")
        };
    }

    private static NotificationItemDto ToItemDto(Notification notification)
    {
        return new NotificationItemDto
        {
            NotificationId = notification.NotificationId,
            Title = notification.Title,
            Content = notification.Content,
            NotificationType = notification.NotificationType,
            ReadTime = notification.ReadTime,
            CreateTime = notification.CreateTime
        };
    }
}
