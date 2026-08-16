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
    public async Task UpdateAsync_RejectsTotalBelowUnreturned()
    {
        // 二轮审核建议项：总数不能低于未归还借出数量（3 笔未归还，改 2 被拒）。
        await using var context = TestDbContextFactory.Create();
        context.SharedItems.Add(new SharedItem { ItemId = 10, ItemName = "球拍", BuildingId = 1, TotalQty = 5, AvailableQty = 2, Status = "正常" });
        context.ItemLoans.AddRange(
            new ItemLoan { LoanId = 1, ItemId = 10, StudentId = "S1", BorrowTime = DateTime.Now.AddDays(-2), DueTime = DateTime.Now.AddDays(-1) },
            new ItemLoan { LoanId = 2, ItemId = 10, StudentId = "S2", BorrowTime = DateTime.Now.AddDays(-2), DueTime = DateTime.Now.AddDays(-1) },
            new ItemLoan { LoanId = 3, ItemId = 10, StudentId = "S3", BorrowTime = DateTime.Now.AddDays(-2), DueTime = DateTime.Now.AddDays(-1) });
        await context.SaveChangesAsync();

        var service = CreateService(context);
        var ex = await Assert.ThrowsAsync<BusinessException>(() =>
            service.UpdateAsync(10, new UpdateSharedItemRequest { Quantity = 2 }, CancellationToken.None));
        Assert.Equal(400, ex.HttpStatus);
    }

    [Fact]
    public async Task UpdateAsync_SyncsAvailableQtyWithUnreturned()
    {
        // 二轮审核建议项：修改总数须按差值同步 AvailableQty（Available = Total - 未归还数）。
        await using var context = TestDbContextFactory.Create();
        context.SharedItems.Add(new SharedItem { ItemId = 10, ItemName = "球拍", BuildingId = 1, TotalQty = 5, AvailableQty = 4, Status = "正常" });
        context.ItemLoans.Add(new ItemLoan { LoanId = 1, ItemId = 10, StudentId = "S1", BorrowTime = DateTime.Now.AddDays(-2), DueTime = DateTime.Now.AddDays(-1) });
        await context.SaveChangesAsync();

        var service = CreateService(context);

        // 增加总数：AvailableQty 同步 +1（6 - 1 未归还 = 5）
        var up = await service.UpdateAsync(10, new UpdateSharedItemRequest { Quantity = 6 }, CancellationToken.None);
        Assert.Equal(6, up.TotalQty);
        Assert.Equal(5, up.AvailableQty);

        // 减少总数：AvailableQty 同步 -1（3 - 1 未归还 = 2）
        var down = await service.UpdateAsync(10, new UpdateSharedItemRequest { Quantity = 3 }, CancellationToken.None);
        Assert.Equal(3, down.TotalQty);
        Assert.Equal(2, down.AvailableQty);
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
    public async Task DeleteAsync_BlocksWhenHasUnreturnedLoan()
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

    [Fact]
    public async Task DeleteAsync_AllowsWhenLoanReturned()
    {
        await using var context = TestDbContextFactory.Create();
        context.SharedItems.Add(new SharedItem { ItemId = 10, ItemName = "球拍", BuildingId = 1, TotalQty = 1, AvailableQty = 1, Status = "正常" });
        context.ItemLoans.Add(new ItemLoan
        {
            LoanId = 1,
            ItemId = 10,
            StudentId = "S1",
            BorrowTime = DateTime.Now.AddDays(-2),
            DueTime = DateTime.Now.AddDays(-1),
            ReturnTime = DateTime.Now
        });
        await context.SaveChangesAsync();

        var service = CreateService(context);
        await service.DeleteAsync(10, CancellationToken.None);

        // 二轮审核阻塞项：已归还的历史借还记录随物品同事务清理（Oracle 外键不阻断删除）。
        Assert.Equal(0, await context.SharedItems.CountAsync());
        Assert.Equal(0, await context.ItemLoans.CountAsync());
    }

    [Fact]
    public async Task CreateAsync_WhitespaceName_Throws()
    {
        await using var context = TestDbContextFactory.Create();
        context.Buildings.Add(new Building { BuildingId = 1 });
        await context.SaveChangesAsync();

        var service = CreateService(context);
        var ex = await Assert.ThrowsAsync<BusinessException>(() =>
            service.CreateAsync(new CreateSharedItemRequest { Name = "   ", Quantity = 1, BuildingId = 1 },
                CancellationToken.None));
        Assert.Equal(400, ex.HttpStatus);
    }

    [Fact]
    public async Task CreateAsync_QuantityAbove999_Throws()
    {
        await using var context = TestDbContextFactory.Create();
        context.Buildings.Add(new Building { BuildingId = 1 });
        await context.SaveChangesAsync();

        var service = CreateService(context);
        var ex = await Assert.ThrowsAsync<BusinessException>(() =>
            service.CreateAsync(new CreateSharedItemRequest { Name = "球拍", Quantity = 1000, BuildingId = 1 },
                CancellationToken.None));
        Assert.Equal(400, ex.HttpStatus);
    }
}
