using Microsoft.AspNetCore.Http;
using TemplateDormApi.DTO;
using TemplateDormApi.Exceptions;
using TemplateDormApi.Models;
using TemplateDormApi.Repository;
using TemplateDormApi.Services;

namespace TemplateDormApi.Tests;

public class NotificationServiceTests
{
    [Fact]
    public async Task CreateAsync_PersistsNotificationForResolvedStudentAccount()
    {
        await using var context = TestDbContextFactory.Create();
        context.UserAccounts.Add(new UserAccount
        {
            AccountId = 101,
            LoginName = "student-101",
            PasswordHash = "not-read-by-test",
            AccountStatus = "正常",
            StudentId = "20260001"
        });
        await context.SaveChangesAsync();

        var service = CreateService(context);
        var result = await service.CreateAsync(new NotificationCreateDto
        {
            StudentId = "20260001",
            Title = "缴费提醒",
            Content = "请及时缴费",
            NotificationType = "账单"
        });

        var stored = Assert.Single(context.Notifications);
        Assert.Equal(101, stored.RecipientAccountId);
        Assert.Equal("缴费提醒", result.Title);
        Assert.Equal(stored.NotificationId, result.NotificationId);
    }

    [Fact]
    public async Task CreateAsync_MissingAccount_Returns40401WithoutPersisting()
    {
        await using var context = TestDbContextFactory.Create();
        var service = CreateService(context);

        var exception = await Assert.ThrowsAsync<BusinessException>(() => service.CreateAsync(new NotificationCreateDto
        {
            StudentId = "missing",
            Title = "系统通知",
            Content = "内容",
            NotificationType = "系统"
        }));

        Assert.Equal(40401, exception.Code);
        Assert.Equal(StatusCodes.Status404NotFound, exception.HttpStatus);
        Assert.Empty(context.Notifications);
    }

    [Fact]
    public async Task MarkReadAsync_IsIdempotentAndDistinguishesMissingNotification()
    {
        await using var context = TestDbContextFactory.Create();
        var notification = new Notification
        {
            NotificationId = 1,
            RecipientAccountId = 101,
            Title = "通知",
            Content = "内容",
            NotificationType = "系统",
            CreateTime = DateTime.Now
        };
        context.Notifications.Add(notification);
        await context.SaveChangesAsync();
        var service = CreateService(context);

        await service.MarkReadAsync(1, 101);
        var firstReadTime = notification.ReadTime;
        await service.MarkReadAsync(1, 101);

        Assert.NotNull(firstReadTime);
        Assert.Equal(firstReadTime, notification.ReadTime);

        var exception = await Assert.ThrowsAsync<BusinessException>(() => service.MarkReadAsync(999, 101));
        Assert.Equal(404, exception.Code);
        Assert.Equal(StatusCodes.Status404NotFound, exception.HttpStatus);
    }

    [Fact]
    public async Task MarkBatchReadAsync_UpdatesOnlyCurrentAccountUnreadNotifications()
    {
        await using var context = TestDbContextFactory.Create();
        var ownUnread = new Notification
        {
            NotificationId = 1,
            RecipientAccountId = 101,
            Title = "未读",
            Content = "内容",
            NotificationType = "系统",
            CreateTime = DateTime.Now
        };
        var ownRead = new Notification
        {
            NotificationId = 2,
            RecipientAccountId = 101,
            Title = "已读",
            Content = "内容",
            NotificationType = "系统",
            ReadTime = DateTime.Now.AddMinutes(-1),
            CreateTime = DateTime.Now
        };
        var anotherAccountUnread = new Notification
        {
            NotificationId = 3,
            RecipientAccountId = 202,
            Title = "其他账户",
            Content = "内容",
            NotificationType = "系统",
            CreateTime = DateTime.Now
        };
        context.Notifications.AddRange(ownUnread, ownRead, anotherAccountUnread);
        await context.SaveChangesAsync();
        var originalReadTime = ownRead.ReadTime;

        await CreateService(context).MarkBatchReadAsync(new[] { 1, 2, 3 }, 101);

        Assert.NotNull(ownUnread.ReadTime);
        Assert.Equal(originalReadTime, ownRead.ReadTime);
        Assert.Null(anotherAccountUnread.ReadTime);
    }

    private static NotificationService CreateService(TemplateDormApi.Data.AppDbContext context)
    {
        return new NotificationService(
            new NotificationRepository(context),
            new UserAccountRepository(context));
    }
}
