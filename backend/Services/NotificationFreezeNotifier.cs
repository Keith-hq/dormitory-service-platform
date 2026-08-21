using TemplateDormApi.DTO;

namespace TemplateDormApi.Services;

/// <summary>
/// 通过独立依赖注入作用域投递冻结通知，避免复用信用事务的 DbContext。
/// </summary>
public class NotificationFreezeNotifier : IFreezeNotifier
{
    private readonly IServiceScopeFactory _scopeFactory;

    public NotificationFreezeNotifier(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public async Task NotifyAsync(
        NotificationCreateDto notification,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await using var scope = _scopeFactory.CreateAsyncScope();
        var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();
        await notificationService.CreateAsync(notification);
    }
}
