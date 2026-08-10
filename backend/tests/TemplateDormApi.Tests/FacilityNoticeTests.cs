using TemplateDormApi.DTO;
using TemplateDormApi.Exceptions;
using TemplateDormApi.Models;
using TemplateDormApi.Repository;
using TemplateDormApi.Services;

namespace TemplateDormApi.Tests;

/// <summary>
/// Facility / Notice 模块：分页回填（B7）、默认可用设施（S5）、楼栋预检（S6）、置顶优先列表（S1）。
/// </summary>
public class FacilityNoticeTests
{
    [Fact]
    public async Task FacilityService_GetPagedAsync_FillsPageAndPageSize_AndDefaultsToNormal()
    {
        await using var context = TestDbContextFactory.Create();
        var service = new FacilityService(new FacilityRepository(context), new BuildingRepository(context));

        context.Facilities.AddRange(
            new Facility { BuildingId = 1, FacilityCode = "W-1", FacilityType = "洗衣机", Status = "正常" },
            new Facility { BuildingId = 1, FacilityCode = "B-1", FacilityType = "浴室", Status = "维修" });
        await context.SaveChangesAsync();

        var result = await service.GetPagedAsync(1, 10);

        Assert.Equal(1, result.Page);
        Assert.Equal(10, result.PageSize);
        // S5：未传 status 默认只看"正常"可用设施
        Assert.Equal(1, result.Total);
        Assert.Equal("W-1", Assert.Single(result.Items).FacilityCode);
    }

    [Fact]
    public async Task FacilityService_GetPagedAsync_FiltersByExplicitStatus()
    {
        await using var context = TestDbContextFactory.Create();
        var service = new FacilityService(new FacilityRepository(context), new BuildingRepository(context));

        context.Facilities.AddRange(
            new Facility { BuildingId = 1, FacilityCode = "W-1", FacilityType = "洗衣机", Status = "正常" },
            new Facility { BuildingId = 1, FacilityCode = "W-2", FacilityType = "洗衣机", Status = "维修" });
        await context.SaveChangesAsync();

        var result = await service.GetPagedAsync(1, 10, status: "维修");

        Assert.Equal(1, result.Total);
        Assert.Equal("W-2", Assert.Single(result.Items).FacilityCode);
    }

    [Fact]
    public async Task FacilityService_CreateAsync_ThrowsNotFoundWhenBuildingMissing()
    {
        await using var context = TestDbContextFactory.Create();
        var service = new FacilityService(new FacilityRepository(context), new BuildingRepository(context));

        var ex = await Assert.ThrowsAsync<BusinessException>(() =>
            service.CreateAsync(new FacilityCreateDto
            {
                BuildingId = 999,
                FacilityCode = "W-1",
                FacilityType = "洗衣机",
                Status = "正常"
            }));

        Assert.Equal(404, ex.HttpStatus);
    }

    [Fact]
    public async Task NoticeRepository_GetPagedAsync_PinnedFirstThenNewest()
    {
        await using var context = TestDbContextFactory.Create();
        context.Notices.AddRange(
            new Notice { Title = "旧", Content = "c", PublishTime = new DateTime(2026, 1, 1) },
            new Notice
            {
                Title = "置顶新",
                Content = "c",
                PublishTime = new DateTime(2026, 8, 1),
                Display = new NoticeDisplay { IsPinned = "是", PinTime = new DateTime(2026, 8, 1) }
            },
            new Notice { Title = "中间", Content = "c", PublishTime = new DateTime(2026, 6, 1) });
        await context.SaveChangesAsync();

        var repo = new NoticeRepository(context);
        var (items, total) = await repo.GetPagedAsync(1, 10);

        Assert.Equal(3, total);
        Assert.Equal("置顶新", items[0].Title);
        Assert.Equal("中间", items[1].Title);
        Assert.Equal("旧", items[2].Title);
    }

    [Fact]
    public async Task NoticeService_CreateAsync_WithPinnedCreatesDisplay()
    {
        await using var context = TestDbContextFactory.Create();
        var service = new NoticeService(new NoticeRepository(context));

        var notice = await service.CreateAsync(new NoticeCreateDto
        {
            Title = "开学通知",
            Content = "请准时到校。",
            IsPinned = "是"
        });

        Assert.True(notice.NoticeId > 0);
        Assert.NotNull(notice.Display);
        Assert.Equal("是", notice.Display!.IsPinned);
        Assert.Equal(notice.NoticeId, notice.Display.NoticeId);
    }

    [Fact]
    public async Task NoticeService_GetPagedAsync_FillsPageAndPageSize()
    {
        await using var context = TestDbContextFactory.Create();
        context.Notices.Add(new Notice { Title = "t", Content = "c", PublishTime = DateTime.Now });
        await context.SaveChangesAsync();

        var service = new NoticeService(new NoticeRepository(context));
        var result = await service.GetPagedAsync(2, 5);

        Assert.Equal(2, result.Page);
        Assert.Equal(5, result.PageSize);
        Assert.Equal(1, result.Total);
    }
}
