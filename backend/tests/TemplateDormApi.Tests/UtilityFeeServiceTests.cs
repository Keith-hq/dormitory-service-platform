using Microsoft.AspNetCore.Http;
using TemplateDormApi.Data;
using TemplateDormApi.DTO;
using TemplateDormApi.Exceptions;
using TemplateDormApi.Models;
using TemplateDormApi.Services;

namespace TemplateDormApi.Tests;

/// <summary>
/// 水电账单服务层测试（难点②）：InMemory 覆盖可脱离 Oracle 的快路径——
/// 存在性/唯一性/必填兜底/分摊明细存在性校验、列表过滤与分页、明细投影；
/// 条件 UPDATE 的并发守门路径（发布/分摊竞态）需真实 Oracle（由 SP 层测试 + 集成链路覆盖）。
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

    [Theory]
    [MemberData(nameof(MissingFeesCases))]
    public async Task CreateBill_MissingFees_Throws400(decimal? waterFee, decimal? elecFee)
    {
        // PR #58 P2：必填兜底——DTO [Required] 之外，服务层拦截绕过绑定的直接调用
        var context = TestDbContextFactory.Create();
        var service = CreateService(context);

        var ex = await Assert.ThrowsAsync<BusinessException>(() =>
            service.CreateBill(new CreateUtilityFeeRequest
            {
                RoomId = 900101,
                YearMonth = "2026-07",
                WaterFee = waterFee,
                ElecFee = elecFee
            }));

        Assert.Equal(400, ex.Code);
        Assert.Contains("必填", ex.Message);
    }

    public static IEnumerable<object?[]> MissingFeesCases => new[]
    {
        new object?[] { null, 70m },
        new object?[] { 30m, null }
    };

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
    public async Task UpdateBill_Allocated_Throws400()
    {
        // DORM-20 口径（PR #58 P2）：已分摊（存在明细）即禁止修改，
        // 比契约"已发布且已缴"更严格——已分摊未缴也禁止，防账单与明细脱节
        var context = TestDbContextFactory.Create();
        context.UtilityFees.Add(new UtilityFee
        {
            FeeId = 1, RoomId = 900101, YearMonth = "2026-07",
            WaterFee = 30, PowerFee = 70, PublishStatus = "已发布"
        });
        context.FeeDetails.Add(new FeeDetail
        {
            DetailId = 11, FeeId = 1, StudentId = "S001", RoomId = 900101,
            WaterShare = 10, PowerShare = 10, StayDays = 20, TotalDays = 30,
            BillType = "月度", IsPaid = "否"
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

    // ==================== DORM-24 列表过滤与分页 ====================

    [Fact]
    public async Task GetBills_InvalidPublishStatus_Throws400()
    {
        var context = TestDbContextFactory.Create();
        var service = CreateService(context);

        var ex = await Assert.ThrowsAsync<BusinessException>(() =>
            service.GetBills(null, null, null, "草稿", 1, 10));

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

        var publishedJuly = await service.GetBills(null, "2026-07", null, "已发布", 1, 10);
        var single = Assert.Single(publishedJuly.Items);
        Assert.Equal(1, single.FeeId);
        Assert.Equal(1, publishedJuly.Total);
        Assert.Equal(1, publishedJuly.Page);
        Assert.Equal(10, publishedJuly.PageSize);

        var all = await service.GetBills(null, null, null, null, 1, 10);
        Assert.Equal(3, all.Items.Count);
        Assert.Equal(3, all.Total);

        // 排序：账期倒序（2026-07 在前）
        Assert.Equal("2026-07", all.Items[0].YearMonth);
        Assert.Equal("2026-06", all.Items[2].YearMonth);
    }

    [Fact]
    public async Task GetBills_FiltersByBuildingId()
    {
        var context = TestDbContextFactory.Create();
        context.Rooms.AddRange(
            new Room { RoomId = 900101, BuildingId = 1, RoomNumber = "101", Capacity = 4, Status = "正常", PowerStatus = "正常" },
            new Room { RoomId = 900102, BuildingId = 2, RoomNumber = "102", Capacity = 4, Status = "正常", PowerStatus = "正常" });
        context.UtilityFees.AddRange(
            new UtilityFee { FeeId = 1, RoomId = 900101, YearMonth = "2026-07", PublishStatus = "未发布" },
            new UtilityFee { FeeId = 2, RoomId = 900102, YearMonth = "2026-07", PublishStatus = "未发布" });
        await context.SaveChangesAsync();
        var service = CreateService(context);

        var building1 = await service.GetBills(1, null, null, null, 1, 10);
        var single = Assert.Single(building1.Items);
        Assert.Equal(1, single.FeeId);

        var building2 = await service.GetBills(2, null, null, null, 1, 10);
        Assert.Equal(2, Assert.Single(building2.Items).FeeId);
    }

    [Fact]
    public async Task GetBills_FiltersByIsPaid_DrivenByDetails()
    {
        var context = TestDbContextFactory.Create();
        context.UtilityFees.AddRange(
            new UtilityFee { FeeId = 1, RoomId = 900101, YearMonth = "2026-07", PublishStatus = "已发布" },
            new UtilityFee { FeeId = 2, RoomId = 900102, YearMonth = "2026-07", PublishStatus = "已发布" },
            new UtilityFee { FeeId = 3, RoomId = 900103, YearMonth = "2026-07", PublishStatus = "已发布" });
        context.FeeDetails.AddRange(
            new FeeDetail { DetailId = 11, FeeId = 1, StudentId = "S001", RoomId = 900101, IsPaid = "是" },
            new FeeDetail { DetailId = 12, FeeId = 2, StudentId = "S002", RoomId = 900102, IsPaid = "否" },
            new FeeDetail { DetailId = 13, FeeId = 3, StudentId = "S001", RoomId = 900103, IsPaid = "是" },
            new FeeDetail { DetailId = 14, FeeId = 3, StudentId = "S002", RoomId = 900103, IsPaid = "否" });
        await context.SaveChangesAsync();
        var service = CreateService(context);

        // isPaid=true：无未缴明细（Fee1 全部缴清；Fee3 有一笔未缴 → 排除）
        var paid = await service.GetBills(null, null, true, null, 1, 10);
        var paidFee = Assert.Single(paid.Items);
        Assert.Equal(1, paidFee.FeeId);

        // isPaid=false：存在未缴明细（Fee2/Fee3）
        var unpaid = await service.GetBills(null, null, false, null, 1, 10);
        Assert.Equal(new[] { 2L, 3L }, unpaid.Items.Select(i => i.FeeId).OrderBy(x => x));
        Assert.Equal(2, unpaid.Total);
    }

    [Fact]
    public async Task GetBills_Paginates()
    {
        var context = TestDbContextFactory.Create();
        context.UtilityFees.AddRange(
            new UtilityFee { FeeId = 1, RoomId = 900101, YearMonth = "2026-07", PublishStatus = "未发布" },
            new UtilityFee { FeeId = 2, RoomId = 900102, YearMonth = "2026-07", PublishStatus = "未发布" },
            new UtilityFee { FeeId = 3, RoomId = 900103, YearMonth = "2026-07", PublishStatus = "未发布" });
        await context.SaveChangesAsync();
        var service = CreateService(context);

        var page1 = await service.GetBills(null, null, null, null, 1, 2);
        Assert.Equal(2, page1.Items.Count);
        Assert.Equal(3, page1.Total);
        Assert.Equal(2, page1.PageSize);

        var page2 = await service.GetBills(null, null, null, null, 2, 2);
        var last = Assert.Single(page2.Items);
        Assert.Equal(2, page2.Page);
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
