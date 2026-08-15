using Microsoft.EntityFrameworkCore;
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

        var service = new VisitorService(new VisitorRepository(context));
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

        var service = new VisitorService(new VisitorRepository(context));
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

        var service = new VisitorService(new VisitorRepository(context));
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

        var service = new VisitorService(new VisitorRepository(context));
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

        var service = new VisitorService(new VisitorRepository(context));
        var auth = await service.ApplyAsync("S001", new VisitorApplyRequest
        {
            VisitorName = "张三",
            EndTime = DateTime.Now.AddHours(2)
        });

        await Assert.ThrowsAsync<BusinessException>(() =>
            service.RevokeAsync(auth.AuthorizationId, "S002"));
    }
}
