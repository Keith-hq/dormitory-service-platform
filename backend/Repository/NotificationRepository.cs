using Microsoft.EntityFrameworkCore;
using TemplateDormApi.Data;
using TemplateDormApi.Models;

namespace TemplateDormApi.Repository;

/// <summary>
/// 通知数据访问。
/// </summary>
public class NotificationRepository
{
    private const int MaxBatchSize = 1000;
    private readonly AppDbContext _context;

    public NotificationRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<(List<Notification> Items, int Total)> GetPagedAsync(
        int recipientAccountId,
        int page,
        int pageSize,
        bool? isRead)
    {
        IQueryable<Notification> query = _context.Notifications
            .AsNoTracking()
            .Where(notification => notification.RecipientAccountId == recipientAccountId);

        if (isRead.HasValue)
        {
            query = isRead.Value
                ? query.Where(notification => notification.ReadTime != null)
                : query.Where(notification => notification.ReadTime == null);
        }

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(notification => notification.CreateTime)
            .ThenByDescending(notification => notification.NotificationId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, total);
    }

    public async Task<bool> ExistsAsync(int notificationId, int recipientAccountId)
    {
        var count = await _context.Notifications
            .AsNoTracking()
            .CountAsync(notification =>
                notification.NotificationId == notificationId &&
                notification.RecipientAccountId == recipientAccountId);
        return count > 0;
    }

    public async Task<int> MarkReadAsync(int notificationId, int recipientAccountId)
    {
        var query = _context.Notifications.Where(notification =>
            notification.NotificationId == notificationId &&
            notification.RecipientAccountId == recipientAccountId &&
            notification.ReadTime == null);

        return await MarkUnreadAsReadAsync(query);
    }

    public async Task<int> MarkBatchReadAsync(IReadOnlyCollection<int> notificationIds, int recipientAccountId)
    {
        if (notificationIds.Count > MaxBatchSize)
        {
            throw new ArgumentOutOfRangeException(nameof(notificationIds), "批量标记最多支持 1000 条通知。");
        }

        if (notificationIds.Count == 0)
        {
            return 0;
        }

        var notificationIdArray = notificationIds.ToArray();
        var query = _context.Notifications.Where(notification =>
            notification.RecipientAccountId == recipientAccountId &&
            notification.ReadTime == null &&
            notificationIdArray.Contains(notification.NotificationId));

        return await MarkUnreadAsReadAsync(query);
    }

    public Task<int> CountUnreadAsync(int recipientAccountId)
    {
        return _context.Notifications.CountAsync(notification =>
            notification.RecipientAccountId == recipientAccountId &&
            notification.ReadTime == null);
    }

    public async Task<Notification> AddAsync(Notification notification)
    {
        await _context.Notifications.AddAsync(notification);
        await _context.SaveChangesAsync();
        return notification;
    }

    private async Task<int> MarkUnreadAsReadAsync(IQueryable<Notification> query)
    {
        var readTime = DateTime.Now;

        // EF Core InMemory does not translate ExecuteUpdateAsync; production providers use the single SQL UPDATE path.
        if (string.Equals(
                _context.Database.ProviderName,
                "Microsoft.EntityFrameworkCore.InMemory",
                StringComparison.Ordinal))
        {
            var notifications = await query.ToListAsync();
            foreach (var notification in notifications)
            {
                notification.ReadTime = readTime;
            }

            if (notifications.Count > 0)
            {
                await _context.SaveChangesAsync();
            }

            return notifications.Count;
        }

        return await query.ExecuteUpdateAsync(setters =>
            setters.SetProperty(notification => notification.ReadTime, (DateTime?)readTime));
    }
}
