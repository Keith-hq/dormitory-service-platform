using TemplateDormApi.DTO;

namespace TemplateDormApi.Services;

/// <summary>
/// 信用分冻结通知投递边界。
/// </summary>
public interface IFreezeNotifier
{
    Task NotifyAsync(NotificationCreateDto notification, CancellationToken cancellationToken);
}
