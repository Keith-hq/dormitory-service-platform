using Microsoft.EntityFrameworkCore;
using TemplateDormApi.Data;
using TemplateDormApi.DTO;
using TemplateDormApi.Exceptions;
using TemplateDormApi.Models;
using TemplateDormApi.Repository;
using TemplateDormApi.Services;

namespace TemplateDormApi.Tests;

/// <summary>
/// 投票模块：发起投票、参与投票（一人一票）、统计的落库链路。
/// 写入与读取使用共享内存库名、不同 context：读取 context 无跟踪缓存，
/// 可验证「落库」（若属性未持久化，读取 context 将得到空值）。
/// </summary>
public class VoteServiceTests
{
    private static (AppDbContext Write, AppDbContext Read) CreateContexts()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"vote-tests-{Guid.NewGuid():N}")
            .Options;
        return (new AppDbContext(options), new AppDbContext(options));
    }

    [Fact]
    public async Task CreateVoteAndVote_PersistsToDatabase_OnePersonOneVote()
    {
        var (context, readContext) = CreateContexts();
        await using var _ = context;
        await using var __ = readContext;

        // 预置一个房间 + 两名在住成员（发起/投票均校验"本房间在住"）
        var room = new Room { BuildingId = 1, RoomNumber = "101", Status = "正常", PowerStatus = "正常" };
        context.Rooms.Add(room);
        await context.SaveChangesAsync();

        context.BedAllocations.AddRange(
            new BedAllocation { StudentId = "S001", RoomId = room.RoomId, BedNo = 1, CheckInDate = DateTime.Now },
            new BedAllocation { StudentId = "S002", RoomId = room.RoomId, BedNo = 2, CheckInDate = DateTime.Now });
        await context.SaveChangesAsync();

        var service = new VoteService(new VoteRepository(context), new RoomRepository(context));

        // STU-29 发起投票
        var vote = await service.CreateVoteAsync("S001", new CreateVoteRequest
        {
            RoomId = room.RoomId,
            Topic = "周末一起聚餐",
            EligibleCount = 4
        });

        Assert.NotEqual(0, vote.VoteId);
        Assert.Equal("进行中", vote.Status);

        // STU-30 参与投票（S002 同意）
        var stats = await service.VoteAsync("S002", vote.VoteId, new SubmitVoteRequest { Choice = "同意" });
        Assert.Equal(1, stats.AgreeCount);
        Assert.Equal(0, stats.DisagreeCount);
        Assert.Equal(1, stats.TotalCount);

        // 一人一票：S002 重复投票应被拒绝
        await Assert.ThrowsAsync<BusinessException>(() =>
            service.VoteAsync("S002", vote.VoteId, new SubmitVoteRequest { Choice = "不同意" }));

        // STU-31 统计
        var finalStats = await service.GetStatisticsAsync(vote.VoteId);
        Assert.Equal(1, finalStats.AgreeCount);

        // 从独立 context 验证落库（而非内存对象）
        var persistedVote = await readContext.RoomVotes.FindAsync(vote.VoteId);
        Assert.NotNull(persistedVote);
        Assert.Equal("周末一起聚餐", persistedVote!.Topic);

        var persistedResponse = await readContext.RoomVoteResponses
            .FirstOrDefaultAsync(r => r.VoteId == vote.VoteId);
        Assert.NotNull(persistedResponse);
        Assert.Equal("同意", persistedResponse!.Choice);
    }

    [Fact]
    public async Task VoteAsync_ClosedVote_Throws()
    {
        var (context, _) = CreateContexts();
        await using var __ = context;

        var room = new Room { BuildingId = 1, RoomNumber = "102", Status = "正常", PowerStatus = "正常" };
        context.Rooms.Add(room);
        await context.SaveChangesAsync();

        context.BedAllocations.Add(
            new BedAllocation { StudentId = "S001", RoomId = room.RoomId, BedNo = 1, CheckInDate = DateTime.Now });
        await context.SaveChangesAsync();

        var service = new VoteService(new VoteRepository(context), new RoomRepository(context));
        var vote = await service.CreateVoteAsync("S001", new CreateVoteRequest
        {
            RoomId = room.RoomId,
            Topic = "是否更换门锁",
            EligibleCount = 4
        });

        // 直接把状态改为已结束，验证状态机校验
        vote.Status = "已结束";
        await context.SaveChangesAsync();

        await Assert.ThrowsAsync<BusinessException>(() =>
            service.VoteAsync("S003", vote.VoteId, new SubmitVoteRequest { Choice = "同意" }));
    }
}
