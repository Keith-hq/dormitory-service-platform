using Microsoft.EntityFrameworkCore;
using System.Text.Encodings.Web;
using System.Text.Json;
using TemplateDormApi.Data;
using TemplateDormApi.DTO;
using TemplateDormApi.Exceptions;
using TemplateDormApi.Models;
using TemplateDormApi.Repository;
using TemplateDormApi.Services;

namespace TemplateDormApi.Tests;

/// <summary>
/// 退宿清算模块：DORM-11/35/36/37/38 状态机与三步校验（IT-C2-001/003/004/005）。
/// 唯一索引/并发令牌路径依赖 Oracle，由 8/14 集成测试覆盖。
/// </summary>
public class CheckoutServiceTests
{
    /// <summary>匿名对象序列化（不转义中文），用于断言响应字段</summary>
    private static readonly JsonSerializerOptions RelaxedJson = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private static string Serialize(object value) => JsonSerializer.Serialize(value, RelaxedJson);
    /// <summary>记录 calc 调用次数的替身（SP_Calc_Checkout_Fee 由李昂提供，单测不落库）</summary>
    private sealed class FakeFeeSharingService : IFeeSharingService
    {
        public int CalcCheckoutCalls { get; private set; }

        public Task CalcMonthlyFee(string yearMonth) => Task.CompletedTask;

        public Task CalcCheckoutFee(string studentId, int allocationId)
        {
            CalcCheckoutCalls++;
            return Task.CompletedTask;
        }

        public Task<List<FeeDetail>> GetFeeDetail(string studentId, string yearMonth)
            => Task.FromResult(new List<FeeDetail>());
    }

    private sealed class Fixture
    {
        public AppDbContext Context { get; }
        public CheckoutService Service { get; }
        public FakeFeeSharingService FeeSharing { get; } = new();

        public Fixture()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase($"checkout-tests-{Guid.NewGuid():N}")
                .Options;
            Context = new AppDbContext(options);
            Service = new CheckoutService(Context, new CheckoutRepository(Context), FeeSharing);

            Context.Rooms.Add(new Room
            {
                RoomId = 101,
                BuildingId = 1,
                RoomNumber = "101",
                Capacity = 4,
                Occupancy = 1,
                Status = "正常",
                PowerStatus = "正常"
            });
            Context.Students.Add(new Student { StudentId = "S001", Name = "张三" });
            Context.BedAllocations.Add(new BedAllocation
            {
                AllocationId = 1,
                StudentId = "S001",
                RoomId = 101,
                BedNo = 1,
                CheckInDate = new DateTime(2026, 8, 1)
            });
            Context.SaveChanges();
        }

        public Task<object> RegisterAsync() => Service.RegisterAsync(1, new CheckoutRegisterDto());
    }

    [Fact]
    public async Task Register_CreatesPendingLog_AndDuplicateIsRejected()
    {
        var f = new Fixture();
        await using var _ = f.Context;

        var created = await f.RegisterAsync();
        var json = Serialize(created);
        Assert.Contains("待清算", json);

        var ex = await Assert.ThrowsAsync<BusinessException>(() => f.RegisterAsync());
        Assert.Contains("已有进行中的退宿申请", ex.Message);
    }

    [Fact]
    public async Task Register_OnCheckedOutAllocation_Throws()
    {
        var f = new Fixture();
        await using var _ = f.Context;

        var alloc = await f.Context.BedAllocations.FindAsync(1);
        alloc!.CheckOutDate = new DateTime(2026, 8, 10);
        await f.Context.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<BusinessException>(() => f.RegisterAsync());
        Assert.Contains("已退宿", ex.Message);
    }

    [Fact]
    public async Task Settle_UnpaidFees_RejectsWithItemizedReasons()
    {
        var f = new Fixture();
        await using var _ = f.Context;

        // 三步校验数据源：欠费（月度）+ 未取快递 + 未归还共享物品
        f.Context.FeeDetails.Add(new FeeDetail
        {
            DetailId = 1,
            FeeId = 1,
            StudentId = "S001",
            BillType = "月度",
            IsPaid = "否"
        });
        f.Context.ParcelRecords.Add(new ParcelRecord
        {
            ParcelId = 1,
            StudentId = "S001",
            ArriveTime = new DateTime(2026, 8, 1)
        });
        f.Context.ItemLoans.Add(new ItemLoan
        {
            LoanId = 1,
            ItemId = 1,
            StudentId = "S001",
            BorrowTime = new DateTime(2026, 7, 1),
            DueTime = new DateTime(2026, 8, 1)
        });
        await f.Context.SaveChangesAsync();

        await f.RegisterAsync();
        var ex = await Assert.ThrowsAsync<BusinessException>(() => f.Service.SettleAsync(1));

        // 未通过项逐项列出（IT-C2-004 ①）
        Assert.Contains("水电费未缴清", ex.Message);
        Assert.Contains("未取快递", ex.Message);
        Assert.Contains("未归还共享物品", ex.Message);

        // 落库：已拒绝 + 逐项校验结果（IT-C2-003 ③）
        var log = await f.Context.CheckoutLogs.FindAsync(1);
        Assert.Equal("已拒绝", log!.Status);
        Assert.Equal("未通过", log.FeeCheck);
        Assert.Equal("未通过", log.ItemCheck);
        Assert.NotNull(log.ResultTime);
    }

    [Fact]
    public async Task Settle_Passes_ExcludesCheckoutBill_WritesDateAndCallsCalcOnce()
    {
        var f = new Fixture();
        await using var _ = f.Context;

        // 退宿账单本身待缴（Is_Paid='否'），不应阻断清算；月度欠费才阻断
        f.Context.FeeDetails.Add(new FeeDetail
        {
            DetailId = 1,
            FeeId = 1,
            StudentId = "S001",
            BillType = "退宿",
            IsPaid = "否"
        });
        await f.Context.SaveChangesAsync();

        await f.RegisterAsync();
        var result = await f.Service.SettleAsync(1);

        var json = Serialize(result);
        Assert.Contains("通过", json);

        var log = await f.Context.CheckoutLogs.FindAsync(1);
        Assert.Equal("待清算", log!.Status); // settle 通过后仍待确认
        Assert.Equal("通过", log.FeeCheck);
        Assert.Equal("通过", log.ItemCheck);

        var alloc = await f.Context.BedAllocations.FindAsync(1);
        Assert.NotNull(alloc!.CheckOutDate); // v0.4.1：settle 先写退宿日期

        Assert.Equal(1, f.FeeSharing.CalcCheckoutCalls); // 调了 calc SP

        // 重复 settle：幂等，不重复校验、不重复调 calc
        var again = await f.Service.SettleAsync(1);
        Assert.Contains("幂等", Serialize(again));
        Assert.Equal(1, f.FeeSharing.CalcCheckoutCalls);
    }

    [Fact]
    public async Task Confirm_ReleasesBedOnce_AndIsIdempotent()
    {
        var f = new Fixture();
        await using var _ = f.Context;

        await f.RegisterAsync();
        await f.Service.SettleAsync(1);

        var confirmed = await f.Service.ConfirmAsync(1, new CheckoutConfirmDto { CheckoutDate = new DateTime(2026, 8, 12) });
        var json = Serialize(confirmed);
        Assert.Contains("已通过", json);

        var room = await f.Context.Rooms.FindAsync(101);
        Assert.Equal(0, room!.Occupancy); // 释放床位

        // 幂等：重复确认不报错、不重复释放（IT-C2-001 ③）
        var again = await f.Service.ConfirmAsync(1, new CheckoutConfirmDto());
        Assert.Contains("已通过", Serialize(again));
        room = await f.Context.Rooms.FindAsync(101);
        Assert.Equal(0, room!.Occupancy);
    }

    [Fact]
    public async Task Confirm_WithoutSettle_Throws()
    {
        var f = new Fixture();
        await using var _ = f.Context;

        await f.RegisterAsync();
        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => f.Service.ConfirmAsync(1, new CheckoutConfirmDto()));
        Assert.Contains("settle", ex.Message);
    }

    [Fact]
    public async Task Cancel_RevertsCheckoutDate_AndWritesAuditEvent()
    {
        var f = new Fixture();
        await using var _ = f.Context;

        await f.RegisterAsync();
        await f.Service.SettleAsync(1); // 写入 CheckOut_Date

        var cancelled = await f.Service.CancelAsync(1);
        var json = Serialize(cancelled);
        Assert.Contains("已取消", json);

        // 床位恢复在住（IT-C2-005 ②）
        var alloc = await f.Context.BedAllocations.FindAsync(1);
        Assert.Null(alloc!.CheckOutDate);

        // 审计事件（IT-C2-005 ③）
        var audit = await f.Context.AuditEvents.SingleAsync();
        Assert.Equal("退宿清算取消", audit.EventType);
        Assert.Equal("D_CHECKOUT_LOG", audit.TargetType);
        Assert.Equal("1", audit.TargetId);

        // 幂等取消
        var again = await f.Service.CancelAsync(1);
        Assert.Contains("已取消", Serialize(again));

        // 已取消后不可确认
        await Assert.ThrowsAsync<BusinessException>(
            () => f.Service.ConfirmAsync(1, new CheckoutConfirmDto()));
    }

    [Fact]
    public async Task Get_ReturnsSummaryWithAllocationSnapshot()
    {
        var f = new Fixture();
        await using var _ = f.Context;

        await f.RegisterAsync();
        var summary = await f.Service.GetAsync(1);
        var json = Serialize(summary);
        Assert.Contains("待清算", json);
        Assert.Contains("S001", json);
    }
}
