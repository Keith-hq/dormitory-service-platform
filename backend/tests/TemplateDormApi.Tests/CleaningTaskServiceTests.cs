using Microsoft.EntityFrameworkCore;
using TemplateDormApi.Data;
using TemplateDormApi.Exceptions;
using TemplateDormApi.Models;
using TemplateDormApi.Services;

namespace TemplateDormApi.Tests;

/// <summary>
/// 保洁任务（DORM-39/40）：分页列表（联查设施编号）、完成状态机与幂等。
/// </summary>
public class CleaningTaskServiceTests
{
    private static CleaningTaskService CreateService(AppDbContext context) => new(context);

    [Fact]
    public async Task GetPaged_ReturnsTasks_WithFacilityCode()
    {
        await using var context = TestDbContextFactory.Create();
        context.Facilities.Add(new Facility { FacilityId = 1, BuildingId = 1, FacilityCode = "WASHER-01", FacilityType = "洗衣机", Status = "正常" });
        context.CleaningTasks.AddRange(
            new CleaningTask { TaskId = 1, FacilityId = 1, TriggerCount = 1, Status = "待处理", CreateTime = new DateTime(2026, 8, 1) },
            new CleaningTask { TaskId = 2, FacilityId = 1, TriggerCount = 2, Status = "已完成", CreateTime = new DateTime(2026, 8, 2) });
        await context.SaveChangesAsync();

        var service = CreateService(context);
        var result = await service.GetPagedAsync(1, 10, CancellationToken.None);

        Assert.Equal(2, result.Total);
        Assert.All(result.Items, item => Assert.Equal("WASHER-01", item.FacilityCode));
    }

    [Fact]
    public async Task Complete_TransitionsToDone_WithCompleteTime()
    {
        await using var context = TestDbContextFactory.Create();
        context.CleaningTasks.Add(new CleaningTask { TaskId = 1, FacilityId = 1, TriggerCount = 1, Status = "待处理", CreateTime = DateTime.Now });
        await context.SaveChangesAsync();

        var service = CreateService(context);
        var dto = await service.CompleteAsync(1, CancellationToken.None);

        Assert.Equal("已完成", dto.Status);
        Assert.NotNull(dto.CompleteTime);
    }

    [Fact]
    public async Task Complete_IsIdempotentWhenAlreadyDone()
    {
        await using var context = TestDbContextFactory.Create();
        context.CleaningTasks.Add(new CleaningTask { TaskId = 1, FacilityId = 1, TriggerCount = 1, Status = "已完成", CreateTime = DateTime.Now, CompleteTime = new DateTime(2026, 8, 1) });
        await context.SaveChangesAsync();

        var service = CreateService(context);
        var dto = await service.CompleteAsync(1, CancellationToken.None);

        Assert.Equal("已完成", dto.Status);
        Assert.Equal(new DateTime(2026, 8, 1), dto.CompleteTime);
    }

    [Fact]
    public async Task Complete_ThrowsWhenTaskMissing()
    {
        await using var context = TestDbContextFactory.Create();
        var service = CreateService(context);

        var ex = await Assert.ThrowsAsync<BusinessException>(() => service.CompleteAsync(999, CancellationToken.None));
        Assert.Equal(404, ex.HttpStatus);
    }
}
