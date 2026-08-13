using System.Data;
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

    public Task<bool> ExistsAsync(int notificationId, int recipientAccountId)
    {
        return _context.Notifications
            .AsNoTracking()
            .AnyAsync(notification =>
                notification.NotificationId == notificationId &&
                notification.RecipientAccountId == recipientAccountId);
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
        await AssignNotificationKeyAsync(notification);
        await _context.Notifications.AddAsync(notification);
        await _context.SaveChangesAsync();
        return notification;
    }

    /// <summary>
    /// 四审 P1-3：真实 Oracle 实测——Oracle 提供方对 ValueGeneratedOnAdd 列一律由
    /// 数据库生成值并用 RETURNING 读回（序列生成器不预取 NEXTVAL），而
    /// D_Notification.NOTIFICATION_ID 列没有默认值 → ORA-01400。主键改为应用层
    /// 预取 SEQ_NOTIFICATION.NEXTVAL 显式赋值，EF 会随 INSERT 写入（与 SP 层取值
    /// 风格一致，序列并发安全；应用层唯一写入方，无 MAX+1 直写共存风险）。
    /// InMemory 保持 EF 自动生成。
    /// 预取走连接级裸命令：SingleAsync 会把原始 SQL 组合为子查询，
    /// NEXTVAL 在子查询中非法（ORA-02287，真实 Oracle 实测）。
    /// </summary>
    private async Task AssignNotificationKeyAsync(Notification notification)
    {
        if (string.Equals(
                _context.Database.ProviderName,
                "Microsoft.EntityFrameworkCore.InMemory",
                StringComparison.Ordinal))
        {
            return;
        }

        var connection = _context.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync();
        }

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT SEQ_NOTIFICATION.NEXTVAL FROM DUAL";
        var value = await command.ExecuteScalarAsync();
        notification.NotificationId = Convert.ToInt32(value);
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
