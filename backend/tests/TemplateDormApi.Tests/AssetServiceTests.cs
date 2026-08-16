using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using TemplateDormApi.Data;
using TemplateDormApi.DTO;
using TemplateDormApi.Exceptions;
using TemplateDormApi.Models;
using TemplateDormApi.Repository;
using TemplateDormApi.Services;

namespace TemplateDormApi.Tests;

/// <summary>
/// 资产管理（DORM-12~18）：登记/列表/修改/删除阻断/盘点/转报修（防重复+生成工单）/
/// 预警列表/处理。
/// </summary>
public class AssetServiceTests
{
    private static AssetService CreateService(AppDbContext context)
        => new(context, new RepairRepository(context), new AuditService(context, new HttpContextAccessor()));

    [Fact]
    public async Task CreateAsync_RegistersAsset_WithDefaults()
    {
        await using var context = TestDbContextFactory.Create();
        context.Rooms.Add(new Room { RoomId = 1, RoomNumber = "101" });
        await context.SaveChangesAsync();

        var service = CreateService(context);
        var dto = await service.CreateAsync(
            new CreateAssetRequest { RoomId = 1, AssetName = "空调", Quantity = 2 },
            CancellationToken.None);

        Assert.True(dto.AssetId > 0);
        Assert.Equal("空调", dto.AssetName);
        Assert.Equal(2, dto.Quantity);
        Assert.Equal("正常", dto.Status);
    }

    [Fact]
    public async Task CreateAsync_ThrowsWhenRoomMissing()
    {
        await using var context = TestDbContextFactory.Create();
        var service = CreateService(context);

        var ex = await Assert.ThrowsAsync<BusinessException>(() =>
            service.CreateAsync(new CreateAssetRequest { RoomId = 999, AssetName = "床", Quantity = 1 },
                CancellationToken.None));

        Assert.Equal(404, ex.HttpStatus);
    }

    [Fact]
    public async Task CreateAsync_RejectsInvalidStatus()
    {
        await using var context = TestDbContextFactory.Create();
        context.Rooms.Add(new Room { RoomId = 1, RoomNumber = "101" });
        await context.SaveChangesAsync();

        var service = CreateService(context);
        var ex = await Assert.ThrowsAsync<BusinessException>(() =>
            service.CreateAsync(new CreateAssetRequest { RoomId = 1, AssetName = "空调", Quantity = 1, Status = "报废" },
                CancellationToken.None));

        Assert.Equal(400, ex.HttpStatus);
    }

    [Fact]
    public async Task GetByRoom_ReturnsAssetsOfRoom()
    {
        await using var context = TestDbContextFactory.Create();
        context.Assets.AddRange(
            new Asset { RoomId = 1, AssetName = "空调", Quantity = 1, Status = "正常" },
            new Asset { RoomId = 1, AssetName = "床", Quantity = 2, Status = "正常" },
            new Asset { RoomId = 2, AssetName = "桌子", Quantity = 1, Status = "正常" });
        await context.SaveChangesAsync();

        var service = CreateService(context);
        var items = await service.GetByRoomAsync(1, CancellationToken.None);

        Assert.Equal(2, items.Count);
        Assert.All(items, item => Assert.Equal(1, item.RoomId));
    }

    [Fact]
    public async Task UpdateAsync_UpdatesNameAndStatus()
    {
        await using var context = TestDbContextFactory.Create();
        context.Assets.Add(new Asset { AssetId = 10, RoomId = 1, AssetName = "空调", Quantity = 1, Status = "正常" });
        await context.SaveChangesAsync();

        var service = CreateService(context);
        var dto = await service.UpdateAsync(10, new UpdateAssetRequest { AssetName = "新空调", Status = "损坏" },
            CancellationToken.None);

        Assert.Equal("新空调", dto.AssetName);
        Assert.Equal("损坏", dto.Status);
    }

    [Fact]
    public async Task UpdateAsync_RejectsInvalidStatus()
    {
        await using var context = TestDbContextFactory.Create();
        context.Assets.Add(new Asset { AssetId = 10, RoomId = 1, AssetName = "空调", Quantity = 1, Status = "正常" });
        await context.SaveChangesAsync();

        var service = CreateService(context);
        var ex = await Assert.ThrowsAsync<BusinessException>(() =>
            service.UpdateAsync(10, new UpdateAssetRequest { Status = "报废" }, CancellationToken.None));
        Assert.Equal(400, ex.HttpStatus);
    }

    [Fact]
    public async Task DeleteAsync_DeletesWithoutRepairLink()
    {
        await using var context = TestDbContextFactory.Create();
        context.Assets.Add(new Asset { AssetId = 10, RoomId = 1, AssetName = "空调", Quantity = 1, Status = "正常" });
        await context.SaveChangesAsync();

        var service = CreateService(context);
        await service.DeleteAsync(10, CancellationToken.None);

        Assert.Equal(0, await context.Assets.CountAsync());
    }

    [Fact]
    public async Task DeleteAsync_BlocksWhenLinkedToRepair()
    {
        await using var context = TestDbContextFactory.Create();
        context.Assets.Add(new Asset { AssetId = 10, RoomId = 1, AssetName = "空调", Quantity = 1, Status = "损坏" });
        await context.SaveChangesAsync();

        var service = CreateService(context);
        await service.ToRepairAsync(10, new ToRepairRequest { Description = "空调不制冷，请检修" }, CancellationToken.None);

        var ex = await Assert.ThrowsAsync<BusinessException>(() => service.DeleteAsync(10, CancellationToken.None));
        Assert.Equal(409, ex.HttpStatus);
    }

    [Fact]
    public async Task Stocktake_UpdatesQuantity()
    {
        await using var context = TestDbContextFactory.Create();
        context.Assets.Add(new Asset { AssetId = 10, RoomId = 1, AssetName = "空调", Quantity = 2, Status = "正常" });
        await context.SaveChangesAsync();

        var service = CreateService(context);
        var dto = await service.StocktakeAsync(10, new StocktakeAssetRequest { Quantity = 1 }, CancellationToken.None);

        Assert.Equal(1, dto.Quantity);
    }

    [Fact]
    public async Task ToRepair_OnlyDamagedAssetCanConvert()
    {
        await using var context = TestDbContextFactory.Create();
        context.Assets.Add(new Asset { AssetId = 10, RoomId = 1, AssetName = "空调", Quantity = 1, Status = "正常" });
        await context.SaveChangesAsync();

        var service = CreateService(context);
        var ex = await Assert.ThrowsAsync<BusinessException>(() =>
            service.ToRepairAsync(10, new ToRepairRequest { Description = "空调不制冷，请检修" }, CancellationToken.None));
        Assert.Equal(409, ex.HttpStatus);
    }

    [Fact]
    public async Task ToRepair_CreatesTicketLinkAndWarning()
    {
        await using var context = TestDbContextFactory.Create();
        context.Assets.Add(new Asset { AssetId = 10, RoomId = 1, AssetName = "空调", Quantity = 1, Status = "损坏" });
        await context.SaveChangesAsync();

        var service = CreateService(context);
        var warning = await service.ToRepairAsync(
            10, new ToRepairRequest { Description = "空调不制冷，请检修" }, CancellationToken.None);

        Assert.Equal("否", warning.Handled);
        Assert.Equal(1, await context.RepairTickets.CountAsync());
        Assert.Equal(1, await context.AssetRepairs.CountAsync());
        Assert.Equal(1, await context.AssetWarnings.CountAsync());

        var ticket = await context.RepairTickets.SingleAsync();
        Assert.Equal("待处理", ticket.Status);
        Assert.Equal(1, ticket.RoomId);
        Assert.Null(ticket.StudentId);
    }

    [Fact]
    public async Task ToRepair_BlocksDuplicateOpenTicket()
    {
        await using var context = TestDbContextFactory.Create();
        context.Assets.Add(new Asset { AssetId = 10, RoomId = 1, AssetName = "空调", Quantity = 1, Status = "损坏" });
        await context.SaveChangesAsync();

        var service = CreateService(context);
        await service.ToRepairAsync(10, new ToRepairRequest { Description = "空调不制冷，请检修" }, CancellationToken.None);

        var ex = await Assert.ThrowsAsync<BusinessException>(() =>
            service.ToRepairAsync(10, new ToRepairRequest { Description = "再次报修" }, CancellationToken.None));
        Assert.Equal(409, ex.HttpStatus);
    }

    [Fact]
    public async Task Warnings_ListAndHandleLifecycle()
    {
        await using var context = TestDbContextFactory.Create();
        context.Assets.Add(new Asset { AssetId = 10, RoomId = 1, AssetName = "空调", Quantity = 1, Status = "损坏" });
        await context.SaveChangesAsync();

        var service = CreateService(context);
        await service.ToRepairAsync(10, new ToRepairRequest { Description = "空调不制冷，请检修" }, CancellationToken.None);

        var list = await service.GetWarningsAsync(1, 10, CancellationToken.None);
        Assert.Equal(1, list.Total);
        Assert.Equal("空调", Assert.Single(list.Items).AssetName);

        var handled = await service.HandleWarningAsync(10, new HandleWarningRequest { Action = "处理" }, CancellationToken.None);
        Assert.Equal("是", handled.Handled);
        Assert.Equal("处理", handled.HandleAction);

        var list2 = await service.GetWarningsAsync(1, 10, CancellationToken.None);
        Assert.Equal(0, list2.Total);
    }

    [Fact]
    public async Task HandleWarning_ThrowsWhenNoPendingWarning()
    {
        await using var context = TestDbContextFactory.Create();
        context.Assets.Add(new Asset { AssetId = 10, RoomId = 1, AssetName = "空调", Quantity = 1, Status = "正常" });
        await context.SaveChangesAsync();

        var service = CreateService(context);
        var ex = await Assert.ThrowsAsync<BusinessException>(() =>
            service.HandleWarningAsync(10, new HandleWarningRequest { Action = "处理" }, CancellationToken.None));
        Assert.Equal(404, ex.HttpStatus);
    }

    [Fact]
    public async Task CreateAsync_DamagedAsset_GeneratesWarning()
    {
        await using var context = TestDbContextFactory.Create();
        context.Rooms.Add(new Room { RoomId = 1, RoomNumber = "101" });
        await context.SaveChangesAsync();

        var service = CreateService(context);
        await service.CreateAsync(
            new CreateAssetRequest { RoomId = 1, AssetName = "空调", Quantity = 1, Status = "损坏" },
            CancellationToken.None);

        Assert.Equal(1, await context.AssetWarnings.CountAsync());
    }

    [Fact]
    public async Task CreateAsync_MissingAsset_GeneratesWarning()
    {
        await using var context = TestDbContextFactory.Create();
        context.Rooms.Add(new Room { RoomId = 1, RoomNumber = "101" });
        await context.SaveChangesAsync();

        var service = CreateService(context);
        await service.CreateAsync(
            new CreateAssetRequest { RoomId = 1, AssetName = "微波炉", Quantity = 1, Status = "缺失" },
            CancellationToken.None);

        Assert.Equal(1, await context.AssetWarnings.CountAsync());
    }

    [Fact]
    public async Task CreateAsync_WhitespaceName_Throws()
    {
        await using var context = TestDbContextFactory.Create();
        context.Rooms.Add(new Room { RoomId = 1, RoomNumber = "101" });
        await context.SaveChangesAsync();

        var service = CreateService(context);
        var ex = await Assert.ThrowsAsync<BusinessException>(() =>
            service.CreateAsync(new CreateAssetRequest { RoomId = 1, AssetName = "   ", Quantity = 1 },
                CancellationToken.None));
        Assert.Equal(400, ex.HttpStatus);
    }

    [Fact]
    public async Task CreateAsync_QuantityAbove999_Throws()
    {
        await using var context = TestDbContextFactory.Create();
        context.Rooms.Add(new Room { RoomId = 1, RoomNumber = "101" });
        await context.SaveChangesAsync();

        var service = CreateService(context);
        var ex = await Assert.ThrowsAsync<BusinessException>(() =>
            service.CreateAsync(new CreateAssetRequest { RoomId = 1, AssetName = "空调", Quantity = 1000 },
                CancellationToken.None));
        Assert.Equal(400, ex.HttpStatus);
    }

    [Fact]
    public async Task UpdateAsync_StatusToDamaged_GeneratesWarning()
    {
        await using var context = TestDbContextFactory.Create();
        context.Assets.Add(new Asset { AssetId = 10, RoomId = 1, AssetName = "空调", Quantity = 1, Status = "正常" });
        await context.SaveChangesAsync();

        var service = CreateService(context);
        await service.UpdateAsync(10, new UpdateAssetRequest { Status = "损坏" }, CancellationToken.None);

        Assert.Equal(1, await context.AssetWarnings.CountAsync());
    }

    [Fact]
    public async Task UpdateAsync_AlreadyDamaged_NoDuplicateWarning()
    {
        await using var context = TestDbContextFactory.Create();
        context.Assets.Add(new Asset { AssetId = 10, RoomId = 1, AssetName = "空调", Quantity = 1, Status = "损坏" });
        await context.SaveChangesAsync();

        var service = CreateService(context);
        await service.UpdateAsync(10, new UpdateAssetRequest { AssetName = "新空调" }, CancellationToken.None);

        Assert.Equal(0, await context.AssetWarnings.CountAsync());
    }

    [Fact]
    public async Task Stocktake_WithNote_WritesAudit()
    {
        await using var context = TestDbContextFactory.Create();
        context.Assets.Add(new Asset { AssetId = 10, RoomId = 1, AssetName = "空调", Quantity = 2, Status = "正常" });
        await context.SaveChangesAsync();

        var service = CreateService(context);
        await service.StocktakeAsync(10,
            new StocktakeAssetRequest { Quantity = 1, Note = "盘点发现少一台" }, CancellationToken.None);

        var audit = await context.AuditEvents.SingleAsync();
        Assert.Equal("STOCKTAKE", audit.EventType);
        Assert.Equal("10", audit.TargetId);
        Assert.Equal("盘点发现少一台", audit.Details);
    }

    [Fact]
    public async Task Stocktake_WithoutNote_StillWritesAudit()
    {
        await using var context = TestDbContextFactory.Create();
        context.Assets.Add(new Asset { AssetId = 10, RoomId = 1, AssetName = "空调", Quantity = 2, Status = "正常" });
        await context.SaveChangesAsync();

        var service = CreateService(context);
        await service.StocktakeAsync(10, new StocktakeAssetRequest { Quantity = 1 }, CancellationToken.None);

        Assert.Equal(1, await context.AuditEvents.CountAsync());
    }

    [Fact]
    public async Task Stocktake_QuantityAbove999_Throws()
    {
        await using var context = TestDbContextFactory.Create();
        context.Assets.Add(new Asset { AssetId = 10, RoomId = 1, AssetName = "空调", Quantity = 2, Status = "正常" });
        await context.SaveChangesAsync();

        var service = CreateService(context);
        var ex = await Assert.ThrowsAsync<BusinessException>(() =>
            service.StocktakeAsync(10, new StocktakeAssetRequest { Quantity = 1000 }, CancellationToken.None));
        Assert.Equal(400, ex.HttpStatus);
    }
}
