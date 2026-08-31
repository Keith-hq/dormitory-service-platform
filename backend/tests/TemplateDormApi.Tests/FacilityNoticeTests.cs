using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using TemplateDormApi.DTO;
using TemplateDormApi.Exceptions;
using TemplateDormApi.Models;
using TemplateDormApi.Repository;
using TemplateDormApi.Services;
using TemplateDormApi.Controllers;

namespace TemplateDormApi.Tests;

/// <summary>
/// Facility / Notice 模块：分页回填（B7）、默认可用设施（S5）、楼栋预检（S6）、置顶优先列表（S1）。
/// </summary>
public class FacilityNoticeTests
{
    /// <summary>NoticeService 构造依赖 IHttpContextAccessor（发布人取 JWT Name claim），测试提供默认上下文。</summary>
    private static IHttpContextAccessor CreateHttpContextAccessor(string? name = "IT_ADMIN_001")
    {
        var httpContext = new DefaultHttpContext();
        if (name is not null)
        {
            httpContext.User = new ClaimsPrincipal(
                new ClaimsIdentity(new[] { new Claim(ClaimTypes.Name, name) }));
        }
        return new HttpContextAccessor { HttpContext = httpContext };
    }

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
                IsPinned = "是",
                PinTime = new DateTime(2026, 8, 1)
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
    public async Task NoticeService_CreateAsync_WithPinnedWritesPinColumns()
    {
        await using var context = TestDbContextFactory.Create();
        var service = new NoticeService(new NoticeRepository(context), CreateHttpContextAccessor());

        var notice = await service.CreateAsync(new NoticeCreateDto
        {
            Title = "开学通知",
            Content = "请准时到校。",
            IsPinned = "是"
        });

        // 041 起置顶两列并入公告本体（不再创建 D_Notice_Display 卫星行）
        Assert.True(notice.NoticeId > 0);
        Assert.Equal("是", notice.IsPinned);
        Assert.NotNull(notice.PinTime);
    }

    [Fact]
    public async Task NoticeService_GetPagedAsync_FillsPageAndPageSize()
    {
        await using var context = TestDbContextFactory.Create();
        context.Notices.Add(new Notice { Title = "t", Content = "c", PublishTime = DateTime.Now });
        await context.SaveChangesAsync();

        var service = new NoticeService(new NoticeRepository(context), CreateHttpContextAccessor());
        var result = await service.GetPagedAsync(2, 5);

        Assert.Equal(2, result.Page);
        Assert.Equal(5, result.PageSize);
        Assert.Equal(1, result.Total);
    }

    [Fact]
    public async Task NoticeController_GetPagedAsync_ReturnsSerializableDto_WhenNoticeIsPinned()
    {
        await using var context = TestDbContextFactory.Create();
        context.Notices.Add(new Notice
        {
            AdminId = "A2026001",
            Title = "置顶公告",
            Content = "请完成交接。",
            PublishTime = new DateTime(2026, 8, 20, 9, 0, 0),
            IsPinned = "是",
            PinTime = new DateTime(2026, 8, 20, 10, 0, 0)
        });
        await context.SaveChangesAsync();

        var controller = new NoticeController(new NoticeService(new NoticeRepository(context), CreateHttpContextAccessor()));
        var actionResult = await controller.GetPaged(1, 5);

        var ok = Assert.IsType<OkObjectResult>(actionResult.Result);
        var response = Assert.IsType<ApiResponse<PagedResult<NoticeItemDto>>>(ok.Value);
        var notice = Assert.Single(response.Data!.Items);
        Assert.Equal("置顶公告", notice.Title);
        Assert.Equal("是", notice.IsPinned);

        var json = JsonSerializer.Serialize(response);
        Assert.Contains(nameof(NoticeItemDto.IsPinned), json);
        Assert.DoesNotContain("display", json, StringComparison.OrdinalIgnoreCase);
    }
}
