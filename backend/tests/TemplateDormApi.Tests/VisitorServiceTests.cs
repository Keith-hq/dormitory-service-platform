using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using TemplateDormApi.Data;
using TemplateDormApi.DTO;
using TemplateDormApi.Exceptions;
using TemplateDormApi.Models;
using TemplateDormApi.Repository;
using TemplateDormApi.Services;

namespace TemplateDormApi.Tests;

/// <summary>
/// 访客授权模块：申请（需在住房间）、撤销、越权校验。
/// </summary>
public class VisitorServiceTests
{
    private static (AppDbContext Write, AppDbContext Read) CreateContexts()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"visitor-tests-{Guid.NewGuid():N}")
            .Options;
        return (new AppDbContext(options), new AppDbContext(options));
    }

    [Fact]
    public async Task ApplyAsync_PersistsWithActiveRoom()
    {
        var (context, readContext) = CreateContexts();
        await using var _ = context;
        await using var __ = readContext;

        context.BedAllocations.Add(new BedAllocation
        {
            StudentId = "S001",
            RoomId = 101,
            BedNo = 1,
            CheckInDate = DateTime.Now
        });
        await context.SaveChangesAsync();

        var service = CreateService(context);
        var auth = await service.ApplyAsync("S001", new VisitorApplyRequest
        {
            VisitorName = "张三",
            VisitReason = "探访",
            EndTime = DateTime.Now.AddHours(2)
        });

        Assert.Equal("有效", auth.Status);
        Assert.Equal(101, auth.RoomId);
        Assert.StartsWith("VSR_", auth.AuthorizationToken);

        var persisted = await readContext.VisitorAuthorizations.FindAsync(auth.AuthorizationId);
        Assert.NotNull(persisted);
        Assert.Equal("张三", persisted!.VisitorName);
    }

    [Fact]
    public async Task ApplyAsync_NoActiveRoom_Throws()
    {
        var (context, _) = CreateContexts();
        await using var __ = context;

        var service = CreateService(context);
        await Assert.ThrowsAsync<BusinessException>(() =>
            service.ApplyAsync("S001", new VisitorApplyRequest
            {
                VisitorName = "张三",
                EndTime = DateTime.Now.AddHours(2)
            }));
    }

    [Fact]
    public async Task ApplyAsync_CheckedOutBed_Throws()
    {
        var (context, _) = CreateContexts();
        await using var __ = context;

        // 已退宿（CheckOutDate 有值）不算在住，与退宿模块口径一致
        context.BedAllocations.Add(new BedAllocation
        {
            StudentId = "S001",
            RoomId = 101,
            BedNo = 1,
            CheckInDate = DateTime.Now.AddDays(-30),
            CheckOutDate = DateTime.Now
        });
        await context.SaveChangesAsync();

        var service = CreateService(context);
        await Assert.ThrowsAsync<BusinessException>(() =>
            service.ApplyAsync("S001", new VisitorApplyRequest
            {
                VisitorName = "张三",
                EndTime = DateTime.Now.AddHours(2)
            }));
    }

    [Fact]
    public async Task RevokeAsync_ChangesStatusToRevoked()
    {
        var (context, readContext) = CreateContexts();
        await using var _ = context;
        await using var __ = readContext;

        context.BedAllocations.Add(new BedAllocation
        {
            StudentId = "S001",
            RoomId = 101,
            BedNo = 1,
            CheckInDate = DateTime.Now
        });
        await context.SaveChangesAsync();

        var service = CreateService(context);
        var auth = await service.ApplyAsync("S001", new VisitorApplyRequest
        {
            VisitorName = "张三",
            EndTime = DateTime.Now.AddHours(2)
        });

        var revoked = await service.RevokeAsync(auth.AuthorizationId, "S001");
        Assert.Equal("已撤销", revoked.Status);

        var persisted = await readContext.VisitorAuthorizations.FindAsync(auth.AuthorizationId);
        Assert.Equal("已撤销", persisted!.Status);
    }

    [Fact]
    public async Task RevokeAsync_OtherStudent_Throws()
    {
        var (context, _) = CreateContexts();
        await using var __ = context;

        context.BedAllocations.Add(new BedAllocation
        {
            StudentId = "S001",
            RoomId = 101,
            BedNo = 1,
            CheckInDate = DateTime.Now
        });
        await context.SaveChangesAsync();

        var service = CreateService(context);
        var auth = await service.ApplyAsync("S001", new VisitorApplyRequest
        {
            VisitorName = "张三",
            EndTime = DateTime.Now.AddHours(2)
        });

        await Assert.ThrowsAsync<BusinessException>(() =>
            service.RevokeAsync(auth.AuthorizationId, "S002"));
    }

    [Fact]
    public async Task ExpireAsync_MarksExpiredAndIsIdempotent()
    {
        var (context, readContext) = CreateContexts();
        await using var _ = context;
        await using var __ = readContext;

        context.VisitorAuthorizations.AddRange(
            new VisitorAuthorization
            {
                StudentId = "S001",
                RoomId = 101,
                VisitorName = "张三",
                AuthorizationToken = "VSR_EXPIRED_1",
                ExpiresTime = DateTime.Now.AddMinutes(-1),
                Status = "有效",
                CreateTime = DateTime.Now
            },
            new VisitorAuthorization
            {
                StudentId = "S001",
                RoomId = 101,
                VisitorName = "李四",
                AuthorizationToken = "VSR_VALID_1",
                ExpiresTime = DateTime.Now.AddHours(1),
                Status = "有效",
                CreateTime = DateTime.Now
            });
        await context.SaveChangesAsync();

        var service = CreateService(context);
        var first = await service.ExpireAsync();
        var second = await service.ExpireAsync();

        Assert.Equal(1, first);
        Assert.Equal(0, second);

        var expired = await readContext.VisitorAuthorizations
            .SingleAsync(v => v.AuthorizationToken == "VSR_EXPIRED_1");
        var valid = await readContext.VisitorAuthorizations
            .SingleAsync(v => v.AuthorizationToken == "VSR_VALID_1");
        Assert.Equal("已过期", expired.Status);
        Assert.Equal("有效", valid.Status);
    }

    // ===== Helpers / Fakes =====

    private static VisitorService CreateService(AppDbContext context)
        => new VisitorService(
            new VisitorRepository(context),
            new FakeCreditService(),
            new FakeNotificationService(),
            NullLogger<VisitorService>.Instance);

    private sealed class FakeCreditService : ICreditService
    {
        public Task<CreditResultDto> DeductAsync(CreditDeductDto dto, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<CreditResultDto> RestoreAsync(
            string studentId, int restoreScore, string eventKey, string reason, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<CreditStatusDto> GetStatusAsync(string studentId, CancellationToken cancellationToken)
            => Task.FromResult(new CreditStatusDto { CurrentScore = 100, IsFrozen = false });

        public Task<CreditViewDto> GetViewAsync(string studentId, int accountId, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<ResetResultDto> ResetMonthlyAsync(int year, int month, CancellationToken cancellationToken)
            => throw new NotSupportedException();
    }

    private sealed class FakeNotificationService : INotificationService
    {
        public Task<PagedResult<NotificationItemDto>> GetPagedAsync(
            int recipientAccountId, int page, int pageSize, string? isRead)
            => throw new NotSupportedException();

        public Task MarkReadAsync(int notificationId, int recipientAccountId)
            => throw new NotSupportedException();

        public Task MarkBatchReadAsync(IReadOnlyCollection<int> notificationIds, int recipientAccountId)
            => throw new NotSupportedException();

        public Task<UnreadCountDto> GetUnreadCountAsync(int recipientAccountId)
            => throw new NotSupportedException();

        public Task<NotificationItemDto> CreateAsync(NotificationCreateDto dto)
            => Task.FromResult(new NotificationItemDto());
    }
}
