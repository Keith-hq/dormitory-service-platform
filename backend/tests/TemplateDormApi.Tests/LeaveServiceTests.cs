using Microsoft.EntityFrameworkCore;
using TemplateDormApi.Data;
using TemplateDormApi.DTO;
using TemplateDormApi.Exceptions;
using TemplateDormApi.Models;
using TemplateDormApi.Repository;
using TemplateDormApi.Services;

namespace TemplateDormApi.Tests;

/// <summary>
/// 离校报备模块：状态机（待批→已通过/已驳回/已撤回）与"修改/撤销仅待批可用"（IT-C2-007）。
/// </summary>
public class LeaveServiceTests
{
    private static (AppDbContext Context, LeaveService Service) CreateService()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"leave-tests-{Guid.NewGuid():N}")
            .Options;
        var context = new AppDbContext(options);
        context.Students.Add(new Student { StudentId = "S001", Name = "张三" });
        context.SaveChanges();
        return (context, new LeaveService(new LeaveRepository(context), context));
    }

    private static LeaveSubmitDto ValidDto() => new()
    {
        StudentId = "S001",
        LeaveDate = new DateTime(2026, 8, 12),
        ReturnDate = new DateTime(2026, 8, 20),
        Destination = "北京"
    };

    [Fact]
    public async Task Submit_CreatesPendingAndUpdatesStats()
    {
        var (context, service) = CreateService();
        await using var _ = context;

        var app = await service.SubmitAsync(ValidDto());

        Assert.Equal("待批", app.Status);
        Assert.True(app.ApplyId > 0);
        Assert.Equal("北京", app.Destination);

        var stats = await service.GetStatsAsync();
        Assert.NotNull(stats);
    }

    [Fact]
    public async Task Submit_RejectsReturnBeforeLeave()
    {
        var (context, service) = CreateService();
        await using var _ = context;

        var dto = ValidDto();
        dto.ReturnDate = dto.LeaveDate.AddDays(-1);

        var ex = await Assert.ThrowsAsync<BusinessException>(() => service.SubmitAsync(dto));
        Assert.Contains("返校日期", ex.Message);
    }

    [Fact]
    public async Task Submit_RejectsUnknownStudent()
    {
        var (context, service) = CreateService();
        await using var _ = context;

        var dto = ValidDto();
        dto.StudentId = "NOBODY";

        var ex = await Assert.ThrowsAsync<BusinessException>(() => service.SubmitAsync(dto));
        Assert.Equal(404, ex.HttpStatus);
    }

    [Fact]
    public async Task Approve_FlipsToApproved_AndSecondApproveFails()
    {
        var (context, service) = CreateService();
        await using var _ = context;

        var app = await service.SubmitAsync(ValidDto());
        var approved = await service.ApproveAsync(app.ApplyId);
        Assert.Equal("已通过", approved.Status);

        // 已审批后不可重复审批
        await Assert.ThrowsAsync<BusinessException>(() => service.ApproveAsync(app.ApplyId));
    }

    [Fact]
    public async Task Reject_RequiresPendingAndRecordsReason()
    {
        var (context, service) = CreateService();
        await using var _ = context;

        var app = await service.SubmitAsync(ValidDto());
        var rejected = await service.RejectAsync(app.ApplyId, "行程冲突");
        Assert.Equal("已驳回", rejected.Status);
        Assert.Equal("行程冲突", rejected.Reason);

        // 已驳回后不可再修改
        await Assert.ThrowsAsync<BusinessException>(() =>
            service.UpdateAsync(app.ApplyId, new LeaveUpdateDto { Destination = "上海" }));
    }

    [Fact]
    public async Task Update_OnlyAllowedWhilePending()
    {
        var (context, service) = CreateService();
        await using var _ = context;

        var app = await service.SubmitAsync(ValidDto());
        var updated = await service.UpdateAsync(app.ApplyId, new LeaveUpdateDto { Destination = "上海" });
        Assert.Equal("上海", updated.Destination);

        await service.ApproveAsync(app.ApplyId);
        await Assert.ThrowsAsync<BusinessException>(() =>
            service.UpdateAsync(app.ApplyId, new LeaveUpdateDto { Destination = "广州" }));
    }

    [Fact]
    public async Task Cancel_OnlyAllowedWhilePending()
    {
        var (context, service) = CreateService();
        await using var _ = context;

        var app = await service.SubmitAsync(ValidDto());
        var withdrawn = await service.CancelAsync(app.ApplyId);
        Assert.Equal("已撤回", withdrawn.Status);

        await Assert.ThrowsAsync<BusinessException>(() => service.CancelAsync(app.ApplyId));
    }

    [Fact]
    public async Task GetByStudent_FiltersToOwnApplications()
    {
        var (context, service) = CreateService();
        await using var _ = context;

        context.Students.Add(new Student { StudentId = "S002", Name = "李四" });
        await context.SaveChangesAsync();

        await service.SubmitAsync(ValidDto());
        var dto2 = ValidDto();
        dto2.StudentId = "S002";
        await service.SubmitAsync(dto2);

        var mine = await service.GetByStudentPagedAsync("S001", 1, 10);
        Assert.Equal(1, mine.Total);
        Assert.Equal("S001", mine.Items.Single().StudentId);

        // COUN-01 全量 + 状态筛选
        var all = await service.GetPagedAsync(1, 10);
        Assert.Equal(2, all.Total);
        var pendingOnly = await service.GetPagedAsync(1, 10, "待批");
        Assert.Equal(2, pendingOnly.Total);
    }
}
