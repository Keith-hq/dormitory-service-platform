using Microsoft.AspNetCore.Http;
using TemplateDormApi.Data;
using TemplateDormApi.DTO;
using TemplateDormApi.Exceptions;
using TemplateDormApi.Models;
using TemplateDormApi.Services;

namespace TemplateDormApi.Tests;

/// <summary>
/// 水电账单服务层测试（难点②）：InMemory 覆盖可脱离 Oracle 的快路径——
/// 存在性/唯一性/发布状态校验、列表过滤、明细投影；
/// 条件 UPDATE 的并发守门路径需真实 Oracle（已由 SP 层测试 + 集成链路覆盖）。
/// </summary>
public class UtilityFeeServiceTests
{
    // ==================== DORM-19 录账单快路径 ====================

    [Fact]
    public async Task CreateBill_RoomNotFound_Throws404()
    {
        var context = TestDbContextFactory.Create();
        var service = CreateService(context);

        var ex = await Assert.ThrowsAsync<BusinessException>(() =>
            service.CreateBill(NewRequest()));

        Assert.Equal(404, ex.Code);
        Assert.Equal(StatusCodes.Status404NotFound, ex.HttpStatus);
    }

    [Fact]
    public async Task CreateBill_DuplicateRoomMonth_Throws400()
    {
        var context = TestDbContextFactory.Create();
        context.Rooms.Add(new Room { RoomId = 900101, RoomNumber = "101", Capacity = 4, Occupancy = 1, Status = "正常", PowerStatus = "正常" });
        context.UtilityFees.Add(new UtilityFee
        {
            FeeId = 1, RoomId = 900101, YearMonth = "2026-07",
            WaterFee = 30, PowerFee = 70, PublishStatus = "未发布"
        });
        await context.SaveChangesAsync();
        var service = CreateService(context);

        var ex = await Assert.ThrowsAsync<BusinessException>(() =>
            service.CreateBill(NewRequest()));

        Assert.Equal(400, ex.Code);
    }

    [Fact]
    public async Task CreateBill_Success_PersistsUnpublishedBill()
    {
        var context = TestDbContextFactory.Create();
        context.Rooms.Add(new Room { RoomId = 900101, RoomNumber = "101", Capacity = 4, Occupancy = 1, Status = "正常", PowerStatus = "正常" });
        await context.SaveChangesAsync();
        var service = CreateService(context);

        var feeId = await service.CreateBill(NewRequest());

        Assert.True(feeId > 0);   // InMemory 下 ValueGeneratedOnAdd 回填
        var bill = Assert.Single(context.UtilityFees.ToList());
        Assert.Equal("未发布", bill.PublishStatus);
        Assert.Equal("否", bill.IsPaid);
        Assert.Equal(70m, bill.PowerFee);   // 契约 elecFee ↔ 表列 Power_Fee
    }

    // ==================== DORM-20 修改账单快路径 ====================

    [Fact]
    public async Task UpdateBill_NotFound_Throws404()
    {
        var context = TestDbContextFactory.Create();
        var service = CreateService(context);

        var ex = await Assert.ThrowsAsync<BusinessException>(() =>
            service.UpdateBill(999, new UpdateUtilityFeeRequest { WaterFee = 1, ElecFee = 1 }));

        Assert.Equal(404, ex.Code);
    }

    [Fact]
    public async Task UpdateBill_Published_Throws400()
    {
        var context = TestDbContextFactory.Create();
        context.UtilityFees.Add(new UtilityFee
        {
            FeeId = 1, RoomId = 900101, YearMonth = "2026-07",
            WaterFee = 30, PowerFee = 70, PublishStatus = "已发布"
        });
        await context.SaveChangesAsync();
        var service = CreateService(context);

        var ex = await Assert.ThrowsAsync<BusinessException>(() =>
            service.UpdateBill(1, new UpdateUtilityFeeRequest { WaterFee = 1, ElecFee = 1 }));

        Assert.Equal(400, ex.Code);
        Assert.Contains("不可修改", ex.Message);
    }

    // ==================== DORM-21/22 发布与分摊快路径 ====================

    [Fact]
    public async Task PublishBill_NotFound_Throws404()
    {
        var context = TestDbContextFactory.Create();
        var service = CreateService(context);

        var ex = await Assert.ThrowsAsync<BusinessException>(() => service.PublishBill(999));

        Assert.Equal(404, ex.Code);
    }

    [Fact]
    public async Task AllocateBill_NotFound_Throws404()
    {
        var context = TestDbContextFactory.Create();
        var service = CreateService(context);

        var ex = await Assert.ThrowsAsync<BusinessException>(() => service.AllocateBill(999));

        Assert.Equal(404, ex.Code);
    }

    [Fact]
    public async Task AllocateBill_Unpublished_Throws400()
    {
        var context = TestDbContextFactory.Create();
        context.UtilityFees.Add(new UtilityFee
        {
            FeeId = 1, RoomId = 900101, YearMonth = "2026-07",
            WaterFee = 30, PowerFee = 70, PublishStatus = "未发布"
        });
        await context.SaveChangesAsync();
        var service = CreateService(context);

        var ex = await Assert.ThrowsAsync<BusinessException>(() => service.AllocateBill(1));

        Assert.Equal(400, ex.Code);
        Assert.Contains("未发布", ex.Message);
    }

    [Fact]
    public async Task AllocateBill_Success_CallsMonthlyCalcWithBillMonth()
    {
        var context = TestDbContextFactory.Create();
        context.UtilityFees.Add(new UtilityFee
        {
            FeeId = 1, RoomId = 900101, YearMonth = "2026-07",
            WaterFee = 30, PowerFee = 70, PublishStatus = "已发布"
        });
        await context.SaveChangesAsync();
        var fakeSharing = new FakeFeeSharingService();
        var service = CreateService(context, fakeSharing);

        var result = await service.AllocateBill(1);

        Assert.Equal("2026-07", fakeSharing.LastYearMonth);
        Assert.Equal(1, result.FeeId);
        Assert.Equal("2026-07", result.YearMonth);
        Assert.Equal(0, result.DetailCount);   // 本账单暂无明细
    }

    // ==================== DORM-23 明细投影 ====================

    [Fact]
    public async Task GetBillDetails_ProjectsTotalAndBillType()
    {
        var context = TestDbContextFactory.Create();
        context.UtilityFees.Add(new UtilityFee
        {
            FeeId = 1, RoomId = 900101, YearMonth = "2026-07",
            WaterFee = 30, PowerFee = 70, PublishStatus = "已发布"
        });
        context.FeeDetails.Add(new FeeDetail
        {
            DetailId = 11, FeeId = 1, StudentId = "S001", RoomId = 900101,
            WaterShare = 12.5m, PowerShare = 7.5m, StayDays = 20, TotalDays = 30,
            BillType = "月度", IsPaid = "否"
        });
        await context.SaveChangesAsync();
        var service = CreateService(context);

        var details = await service.GetBillDetails(1);

        var item = Assert.Single(details.Items);
        Assert.Equal(20m, item.Total);      // 12.5 + 7.5
        Assert.Equal("月度", item.BillType);
        Assert.Equal(20, item.StayDays);
        Assert.Equal(30, item.TotalDays);
    }

    [Fact]
    public async Task GetBillDetails_NotFound_Throws404()
    {
        var context = TestDbContextFactory.Create();
        var service = CreateService(context);

        await Assert.ThrowsAsync<BusinessException>(() => service.GetBillDetails(999));
    }

    // ==================== DORM-24 列表过滤 ====================

    [Fact]
    public async Task GetBills_InvalidPublishStatus_Throws400()
    {
        var context = TestDbContextFactory.Create();
        var service = CreateService(context);

        var ex = await Assert.ThrowsAsync<BusinessException>(() =>
            service.GetBills(null, "草稿"));

        Assert.Equal(400, ex.Code);
    }

    [Fact]
    public async Task GetBills_FiltersByYearMonthAndPublishStatus()
    {
        var context = TestDbContextFactory.Create();
        context.UtilityFees.AddRange(
            new UtilityFee { FeeId = 1, RoomId = 900101, YearMonth = "2026-07", PublishStatus = "已发布" },
            new UtilityFee { FeeId = 2, RoomId = 900102, YearMonth = "2026-07", PublishStatus = "未发布" },
            new UtilityFee { FeeId = 3, RoomId = 900101, YearMonth = "2026-06", PublishStatus = "已发布" });
        await context.SaveChangesAsync();
        var service = CreateService(context);

        var publishedJuly = await service.GetBills("2026-07", "已发布");
        var single = Assert.Single(publishedJuly);
        Assert.Equal(1, single.FeeId);

        var all = await service.GetBills(null, null);
        Assert.Equal(3, all.Count);

        // 排序：账期倒序（2026-07 在前）
        Assert.Equal("2026-07", all[0].YearMonth);
        Assert.Equal("2026-06", all[2].YearMonth);
    }

    // ==================== 辅助 ====================

    private static CreateUtilityFeeRequest NewRequest() => new()
    {
        RoomId = 900101,
        YearMonth = "2026-07",
        WaterFee = 30m,
        ElecFee = 70m
    };

    private static UtilityFeeService CreateService(AppDbContext context, IFeeSharingService? feeSharingService = null)
        => new(context, feeSharingService ?? new FakeFeeSharingService());

    private sealed class FakeFeeSharingService : IFeeSharingService
    {
        public string? LastYearMonth { get; private set; }

        public Task CalcMonthlyFee(string yearMonth)
        {
            LastYearMonth = yearMonth;
            return Task.CompletedTask;
        }

        public Task CalcCheckoutFee(string studentId, int allocationId)
            => throw new NotSupportedException();

        public Task<List<FeeDetail>> GetFeeDetail(string studentId, string yearMonth)
            => throw new NotSupportedException();
    }
}
