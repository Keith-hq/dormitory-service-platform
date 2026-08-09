using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using TemplateDormApi.Data;
using TemplateDormApi.DTO;
using TemplateDormApi.Exceptions;
using TemplateDormApi.Models;
using TemplateDormApi.Repository;
using TemplateDormApi.Services;

namespace TemplateDormApi.Tests;

public class CreditServiceTests
{
    [Fact]
    public async Task DeductAsync_UpdatesScoreWritesLogAndRefreshesTime()
    {
        await using var context = TestDbContextFactory.Create();
        await AddStudentAsync(context, "20260001");
        var oldTime = DateTime.Now.AddDays(-1);
        context.CreditAccounts.Add(new CreditAccount
        {
            StudentId = "20260001",
            CurrentScore = 100,
            UpdatedTime = oldTime
        });
        await context.SaveChangesAsync();

        var result = await CreateService(context).DeductAsync(Request("evt-1"), CancellationToken.None);

        var account = await context.CreditAccounts.AsNoTracking().SingleAsync();
        var log = await context.CreditLogs.AsNoTracking().SingleAsync();
        Assert.Equal(98, result.CurrentScore);
        Assert.Equal(98, account.CurrentScore);
        Assert.True(account.UpdatedTime > oldTime);
        Assert.Equal(-2, log.ScoreChange);
        Assert.Equal("借用逾期", log.Reason);
        Assert.Equal("evt-1", log.EventKey);
    }

    [Fact]
    public async Task DeductAsync_SameEventIsIdempotent()
    {
        await using var context = TestDbContextFactory.Create();
        await AddStudentAsync(context, "20260001");
        var service = CreateService(context);

        var first = await service.DeductAsync(Request("same-event"), CancellationToken.None);
        var second = await service.DeductAsync(Request("same-event"), CancellationToken.None);

        Assert.Equal(98, first.CurrentScore);
        Assert.Equal(first.CurrentScore, second.CurrentScore);
        Assert.Equal(98, (await context.CreditAccounts.AsNoTracking().SingleAsync()).CurrentScore);
        Assert.Equal(1, await context.CreditLogs.CountAsync());
    }

    [Fact]
    public async Task DeductAsync_SameEventWithDifferentContentReturns400()
    {
        await using var context = TestDbContextFactory.Create();
        await AddStudentAsync(context, "20260001");
        var service = CreateService(context);
        await service.DeductAsync(Request("conflict"), CancellationToken.None);

        var changed = Request("conflict");
        changed.Reason = "预约爽约";
        var exception = await Assert.ThrowsAsync<BusinessException>(
            () => service.DeductAsync(changed, CancellationToken.None));

        Assert.Equal(400, exception.Code);
        Assert.Equal("Event_Key 已存在且请求内容不一致", exception.Message);
        Assert.Equal(1, await context.CreditLogs.CountAsync());
    }

    [Fact]
    public async Task DeductAsync_RejectsZeroAndOutOfRangeResultsWithoutLogs()
    {
        await using var context = TestDbContextFactory.Create();
        await AddStudentAsync(context, "20260001");
        context.CreditAccounts.Add(new CreditAccount
        {
            StudentId = "20260001",
            CurrentScore = 1,
            UpdatedTime = DateTime.Now
        });
        await context.SaveChangesAsync();
        var service = CreateService(context);

        var zero = Request("zero");
        zero.ScoreChange = 0;
        Assert.Equal(400, (await Assert.ThrowsAsync<BusinessException>(
            () => service.DeductAsync(zero, CancellationToken.None))).Code);

        var belowZero = Request("below-zero");
        belowZero.ScoreChange = -2;
        Assert.Equal(400, (await Assert.ThrowsAsync<BusinessException>(
            () => service.DeductAsync(belowZero, CancellationToken.None))).Code);

        var account = await context.CreditAccounts.SingleAsync();
        account.CurrentScore = 100;
        await context.SaveChangesAsync();
        var aboveOneHundred = Request("above-one-hundred");
        aboveOneHundred.ScoreChange = 1;
        Assert.Equal(400, (await Assert.ThrowsAsync<BusinessException>(
            () => service.DeductAsync(aboveOneHundred, CancellationToken.None))).Code);
        Assert.Empty(await context.CreditLogs.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task DeductAsync_LazilyCreatesAccountAtOneHundredBeforeChange()
    {
        await using var context = TestDbContextFactory.Create();
        await AddStudentAsync(context, "20260001");

        var result = await CreateService(context).DeductAsync(Request("lazy"), CancellationToken.None);

        Assert.Equal(98, result.CurrentScore);
        Assert.Equal(98, (await context.CreditAccounts.AsNoTracking().SingleAsync()).CurrentScore);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DeductAsync_MissingOrInactiveStudentReturns40401(bool createInactiveAccount)
    {
        await using var context = TestDbContextFactory.Create();
        if (createInactiveAccount)
        {
            await AddStudentAsync(context, "20260001", "停用");
        }

        var exception = await Assert.ThrowsAsync<BusinessException>(
            () => CreateService(context).DeductAsync(Request("missing"), CancellationToken.None));

        Assert.Equal(40401, exception.Code);
        Assert.Equal(StatusCodes.Status404NotFound, exception.HttpStatus);
        Assert.Empty(context.CreditAccounts);
        Assert.Empty(context.CreditLogs);
    }

    [Fact]
    public async Task DeductAsync_CrossingFreezeBoundarySendsOneCreditNotification()
    {
        await using var context = TestDbContextFactory.Create();
        await AddStudentAsync(context, "20260001");
        await AddCreditAccountAsync(context, "20260001", 60);
        var notifier = new RecordingFreezeNotifier();

        var result = await CreateService(context, notifier)
            .DeductAsync(Request("freeze"), CancellationToken.None);

        var notification = Assert.Single(notifier.Notifications);
        Assert.True(result.IsFrozen);
        Assert.Equal("信用", notification.NotificationType);
        Assert.Equal("信用分冻结提醒", notification.Title);
        Assert.Equal("20260001", notification.StudentId);
    }

    [Fact]
    public async Task DeductAsync_AlreadyFrozenDoesNotSendAnotherNotification()
    {
        await using var context = TestDbContextFactory.Create();
        await AddStudentAsync(context, "20260001");
        await AddCreditAccountAsync(context, "20260001", 59);
        var notifier = new RecordingFreezeNotifier();

        await CreateService(context, notifier)
            .DeductAsync(Request("still-frozen"), CancellationToken.None);

        Assert.Empty(notifier.Notifications);
    }

    [Fact]
    public async Task DeductAsync_NotificationFailureDoesNotRollbackDeduction()
    {
        await using var context = TestDbContextFactory.Create();
        await AddStudentAsync(context, "20260001");
        await AddCreditAccountAsync(context, "20260001", 60);

        var result = await CreateService(context, new ThrowingFreezeNotifier())
            .DeductAsync(Request("notification-fails"), CancellationToken.None);

        Assert.Equal(58, result.CurrentScore);
        Assert.Equal(58, (await context.CreditAccounts.AsNoTracking().SingleAsync()).CurrentScore);
        Assert.Single(await context.CreditLogs.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task GetStatusAsync_LazilyCreatesAccountAndReportsFrozenState()
    {
        await using var context = TestDbContextFactory.Create();
        await AddStudentAsync(context, "20260001");
        var service = CreateService(context);

        var initial = await service.GetStatusAsync("20260001", CancellationToken.None);
        Assert.Equal(100, initial.CurrentScore);
        Assert.False(initial.IsFrozen);

        var account = await context.CreditAccounts.SingleAsync();
        account.CurrentScore = 59;
        account.UpdatedTime = DateTime.Now;
        await context.SaveChangesAsync();
        var frozen = await service.GetStatusAsync("20260001", CancellationToken.None);
        Assert.True(frozen.IsFrozen);
    }

    [Fact]
    public async Task ResetMonthlyAsync_ResetsNonFullAccountsAndIsIdempotent()
    {
        await using var context = TestDbContextFactory.Create();
        await AddStudentAsync(context, "20260001", accountId: 1);
        await AddStudentAsync(context, "20260002", accountId: 2);
        await AddCreditAccountAsync(context, "20260001", 80);
        await AddCreditAccountAsync(context, "20260002", 100);
        var service = CreateService(context);

        var first = await service.ResetMonthlyAsync(2026, 8, CancellationToken.None);
        var second = await service.ResetMonthlyAsync(2026, 8, CancellationToken.None);

        Assert.Equal(1, first.Processed);
        Assert.Equal(0, first.Failed);
        Assert.Equal(0, second.Processed);
        var log = await context.CreditLogs.AsNoTracking().SingleAsync();
        Assert.Equal(20, log.ScoreChange);
        Assert.Equal("月度重置", log.Reason);
        Assert.Equal("月度重置:2026-08:20260001", log.EventKey);
        Assert.Equal(2, await context.CreditAccounts.CountAsync(account => account.CurrentScore == 100));
    }

    [Fact]
    public async Task GetViewAsync_RejectsOtherStudentAndAdministratorAccounts()
    {
        await using var context = TestDbContextFactory.Create();
        await AddStudentAsync(context, "20260001", accountId: 1);
        context.UserAccounts.Add(new UserAccount
        {
            AccountId = 2,
            LoginName = "admin-2",
            PasswordHash = "not-read-by-test",
            AccountStatus = "正常",
            AdminId = "A002"
        });
        await context.SaveChangesAsync();
        var service = CreateService(context);

        var otherStudent = await Assert.ThrowsAsync<BusinessException>(
            () => service.GetViewAsync("20269999", 1, CancellationToken.None));
        var administrator = await Assert.ThrowsAsync<BusinessException>(
            () => service.GetViewAsync("20260001", 2, CancellationToken.None));

        Assert.Equal(403, otherStudent.Code);
        Assert.Equal(StatusCodes.Status403Forbidden, otherStudent.HttpStatus);
        Assert.Equal("无权查看他人信用分", otherStudent.Message);
        Assert.Equal(403, administrator.Code);
    }

    [Fact]
    public async Task GetViewAsync_ReturnsLatestFiftyLogsInStableDescendingOrder()
    {
        await using var context = TestDbContextFactory.Create();
        await AddStudentAsync(context, "20260001", accountId: 1);
        await AddCreditAccountAsync(context, "20260001", 58);
        var createTime = new DateTime(2026, 8, 8, 12, 0, 0);
        context.CreditLogs.AddRange(Enumerable.Range(1, 55).Select(id => new CreditLog
        {
            LogId = id,
            StudentId = "20260001",
            ScoreChange = -1,
            Reason = $"流水-{id}",
            EventKey = $"view-{id}",
            CreateTime = createTime
        }));
        await context.SaveChangesAsync();

        var view = await CreateService(context).GetViewAsync("20260001", 1, CancellationToken.None);

        Assert.Equal(58, view.CurrentScore);
        Assert.True(view.IsFrozen);
        Assert.Equal(50, view.Items.Count);
        Assert.Equal("流水-55", view.Items[0].Reason);
        Assert.Equal("流水-6", view.Items[^1].Reason);
    }

    private static CreditDeductDto Request(string eventKey)
    {
        return new CreditDeductDto
        {
            StudentId = "20260001",
            ScoreChange = -2,
            Reason = "借用逾期",
            EventKey = eventKey
        };
    }

    private static CreditService CreateService(
        AppDbContext context,
        IFreezeNotifier? notifier = null)
    {
        return new CreditService(
            new CreditRepository(context),
            new UserAccountRepository(context),
            notifier ?? new RecordingFreezeNotifier(),
            NullLogger<CreditService>.Instance);
    }

    private static async Task AddStudentAsync(
        AppDbContext context,
        string studentId,
        string status = "正常",
        int accountId = 1)
    {
        context.UserAccounts.Add(new UserAccount
        {
            AccountId = accountId,
            LoginName = $"student-{accountId}",
            PasswordHash = "not-read-by-test",
            AccountStatus = status,
            StudentId = studentId
        });
        await context.SaveChangesAsync();
    }

    private static async Task AddCreditAccountAsync(
        AppDbContext context,
        string studentId,
        int score)
    {
        context.CreditAccounts.Add(new CreditAccount
        {
            StudentId = studentId,
            CurrentScore = score,
            UpdatedTime = DateTime.Now.AddMinutes(-1)
        });
        await context.SaveChangesAsync();
    }

    private sealed class RecordingFreezeNotifier : IFreezeNotifier
    {
        public List<NotificationCreateDto> Notifications { get; } = new();

        public Task NotifyAsync(
            NotificationCreateDto notification,
            CancellationToken cancellationToken)
        {
            Notifications.Add(notification);
            return Task.CompletedTask;
        }
    }

    private sealed class ThrowingFreezeNotifier : IFreezeNotifier
    {
        public Task NotifyAsync(
            NotificationCreateDto notification,
            CancellationToken cancellationToken)
        {
            throw new InvalidOperationException("模拟通知中心不可用");
        }
    }
}
