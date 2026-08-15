using Microsoft.EntityFrameworkCore;
using TemplateDormApi.Data;
using TemplateDormApi.DTO;
using TemplateDormApi.Exceptions;
using TemplateDormApi.Models;
using TemplateDormApi.Services;

namespace TemplateDormApi.Tests;

/// <summary>
/// 共享物品主数据（DORM-48/49）：发布（满库存）/数量约束/停用维护/删除阻断。
/// </summary>
public class SharedItemServiceTests
{
    private static SharedItemService CreateService(AppDbContext context) => new(context);

    [Fact]
    public async Task CreateAsync_PublishesItem_WithFullStock()
    {
        await using var context = TestDbContextFactory.Create();
        context.Buildings.Add(new Building { BuildingId = 1 });
        await context.SaveChangesAsync();

        var service = CreateService(context);
        var dto = await service.CreateAsync(new CreateSharedItemRequest
        {
            Name = "羽毛球拍",
            Quantity = 5,
            BuildingId = 1,
            Description = "公用"
        }, CancellationToken.None);

        Assert.True(dto.ItemId > 0);
        Assert.Equal("羽毛球拍", dto.ItemName);
        Assert.Equal(5, dto.TotalQty);
        Assert.Equal(5, dto.AvailableQty);
        Assert.Equal("正常", dto.Status);
        Assert.Equal("公用", dto.Description);
    }

    [Fact]
    public async Task CreateAsync_ThrowsWhenBuildingMissing()
    {
        await using var context = TestDbContextFactory.Create();
        var service = CreateService(context);

        var ex = await Assert.ThrowsAsync<BusinessException>(() =>
            service.CreateAsync(new CreateSharedItemRequest { Name = "球拍", Quantity = 1, BuildingId = 999 },
                CancellationToken.None));
        Assert.Equal(404, ex.HttpStatus);
    }

    [Fact]
    public async Task UpdateAsync_RejectsTotalBelowAvailable()
    {
        await using var context = TestDbContextFactory.Create();
        context.SharedItems.Add(new SharedItem { ItemId = 10, ItemName = "球拍", BuildingId = 1, TotalQty = 5, AvailableQty = 3, Status = "正常" });
        await context.SaveChangesAsync();

        var service = CreateService(context);
        var ex = await Assert.ThrowsAsync<BusinessException>(() =>
            service.UpdateAsync(10, new UpdateSharedItemRequest { Quantity = 2 }, CancellationToken.None));
        Assert.Equal(400, ex.HttpStatus);
    }

    [Fact]
    public async Task UpdateAsync_UpdatesQuantityAndStatus()
    {
        await using var context = TestDbContextFactory.Create();
        context.SharedItems.Add(new SharedItem { ItemId = 10, ItemName = "球拍", BuildingId = 1, TotalQty = 5, AvailableQty = 5, Status = "正常" });
        await context.SaveChangesAsync();

        var service = CreateService(context);
        var dto = await service.UpdateAsync(10, new UpdateSharedItemRequest { Quantity = 6, Status = "停用", Description = "维护中" },
            CancellationToken.None);

        Assert.Equal(6, dto.TotalQty);
        Assert.Equal("停用", dto.Status);
        Assert.Equal("维护中", dto.Description);
    }

    [Fact]
    public async Task UpdateAsync_RejectsInvalidStatus()
    {
        await using var context = TestDbContextFactory.Create();
        context.SharedItems.Add(new SharedItem { ItemId = 10, ItemName = "球拍", BuildingId = 1, TotalQty = 5, AvailableQty = 5, Status = "正常" });
        await context.SaveChangesAsync();

        var service = CreateService(context);
        var ex = await Assert.ThrowsAsync<BusinessException>(() =>
            service.UpdateAsync(10, new UpdateSharedItemRequest { Status = "报废" }, CancellationToken.None));
        Assert.Equal(400, ex.HttpStatus);
    }

    [Fact]
    public async Task DeleteAsync_BlocksWhenHasLoanRecord()
    {
        await using var context = TestDbContextFactory.Create();
        context.SharedItems.Add(new SharedItem { ItemId = 10, ItemName = "球拍", BuildingId = 1, TotalQty = 1, AvailableQty = 0, Status = "正常" });
        context.ItemLoans.Add(new ItemLoan { LoanId = 1, ItemId = 10, StudentId = "S1", BorrowTime = DateTime.Now, DueTime = DateTime.Now.AddDays(1) });
        await context.SaveChangesAsync();

        var service = CreateService(context);
        var ex = await Assert.ThrowsAsync<BusinessException>(() => service.DeleteAsync(10, CancellationToken.None));
        Assert.Equal(409, ex.HttpStatus);
    }

    [Fact]
    public async Task DeleteAsync_DeletesWithoutLoanRecord()
    {
        await using var context = TestDbContextFactory.Create();
        context.SharedItems.Add(new SharedItem { ItemId = 10, ItemName = "球拍", BuildingId = 1, TotalQty = 1, AvailableQty = 1, Status = "正常" });
        await context.SaveChangesAsync();

        var service = CreateService(context);
        await service.DeleteAsync(10, CancellationToken.None);

        Assert.Equal(0, await context.SharedItems.CountAsync());
    }
}
