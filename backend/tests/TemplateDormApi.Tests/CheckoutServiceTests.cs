using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
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
/// 退宿清算模块：DORM-11/35/36/37/38 状态机与两步校验（IT-C2-001/003/004/005）
/// + 归属校验（评审整改：学生仅本人/宿管放行）+ 通知失败不阻断。
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

    /// <summary>记录通知投递的替身（通知域公共服务，单测不落库）；可配置抛非业务异常</summary>
    private sealed class FakeNotificationService : INotificationService
    {
        public List<NotificationCreateDto> Created { get; } = new();

        /// <summary>置 true 时 CreateAsync 抛非业务异常（模拟通知服务故障）</summary>
        public bool ThrowNonBusiness { get; set; }

        public Task<NotificationItemDto> CreateAsync(NotificationCreateDto dto)
        {
            if (ThrowNonBusiness)
                throw new InvalidOperationException("通知服务不可用（非业务异常）");
            Created.Add(dto);
            return Task.FromResult(new NotificationItemDto());
        }

        public Task<PagedResult<NotificationItemDto>> GetPagedAsync(
            int recipientAccountId, int page, int pageSize, string? isRead)
            => Task.FromResult(new PagedResult<NotificationItemDto>());

        public Task MarkReadAsync(int notificationId, int recipientAccountId) => Task.CompletedTask;

        public Task MarkBatchReadAsync(IReadOnlyCollection<int> notificationIds, int recipientAccountId)
            => Task.CompletedTask;

        public Task<UnreadCountDto> GetUnreadCountAsync(int recipientAccountId)
            => Task.FromResult(new UnreadCountDto());
    }

    /// <summary>归属学生 S001 的登录账户（服务层按 accountId 解析学生身份）</summary>
    private const int OwnerAccountId = 101;

    private sealed class Fixture
    {
        public AppDbContext Context { get; }
        public CheckoutService Service { get; }
        public FakeFeeSharingService FeeSharing { get; } = new();
        public FakeNotificationService Notifications { get; } = new();

        public Fixture()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase($"checkout-tests-{Guid.NewGuid():N}")
                .Options;
            Context = new AppDbContext(options);
            // 审计走真实 AuditService（共用同一 Context，验证跨模块公共服务路径）
            Service = new CheckoutService(Context, new CheckoutRepository(Context), FeeSharing, Notifications,
                new AuditService(Context, new HttpContextAccessor()), NullLogger<CheckoutService>.Instance);

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
            Context.UserAccounts.Add(new UserAccount
            {
                AccountId = OwnerAccountId,
                LoginName = "stu-101",
                PasswordHash = "h",
                AccountStatus = "正常",
                StudentId = "S001"
            });
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

        /// <summary>本人（S001 账户）登记；其余账户经参数传入</summary>
        public Task<object> RegisterAsync(int? accountId = OwnerAccountId, bool isDormAdmin = false)
            => Service.RegisterAsync(1, new CheckoutRegisterDto(), accountId, isDormAdmin);
    }

    /// <summary>断言调用以 403 业务异常失败（跨账号归属拦截）</summary>
    private static async Task AssertForbidden(Func<Task> act)
    {
        var ex = await Assert.ThrowsAsync<BusinessException>(act);
        Assert.Equal(403, ex.Code);
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

        var alloc = await f.Context.BedAllocations.FindAsync(1L);
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

        // 两步校验数据源：欠费（月度）+ 未归还共享物品（快递项随 C-048 放弃下线）
        f.Context.FeeDetails.Add(new FeeDetail
        {
            DetailId = 1,
            FeeId = 1,
            StudentId = "S001",
            BillType = "月度",
            IsPaid = "否"
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
        var ex = await Assert.ThrowsAsync<BusinessException>(() => f.Service.SettleAsync(1, OwnerAccountId, false));

        // 未通过项逐项列出（IT-C2-004 ①）
        Assert.Contains("水电费未缴清", ex.Message);
        Assert.Contains("未归还共享物品", ex.Message);
        Assert.DoesNotContain("快递", ex.Message);

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
        var result = await f.Service.SettleAsync(1, OwnerAccountId, false);

        var json = Serialize(result);
        Assert.Contains("通过", json);

        var log = await f.Context.CheckoutLogs.FindAsync(1);
        Assert.Equal("待清算", log!.Status); // settle 通过后仍待确认
        Assert.Equal("通过", log.FeeCheck);
        Assert.Equal("通过", log.ItemCheck);

        var alloc = await f.Context.BedAllocations.FindAsync(1L);
        Assert.NotNull(alloc!.CheckOutDate); // v0.4.1：settle 先写退宿日期

        Assert.Equal(1, f.FeeSharing.CalcCheckoutCalls); // 调了 calc SP

        // 重复 settle：幂等，不重复校验、不重复调 calc
        var again = await f.Service.SettleAsync(1, OwnerAccountId, false);
        Assert.Contains("幂等", Serialize(again));
        Assert.Equal(1, f.FeeSharing.CalcCheckoutCalls);
    }

    [Fact]
    public async Task Confirm_ReleasesBedOnce_AndIsIdempotent()
    {
        var f = new Fixture();
        await using var _ = f.Context;

        await f.RegisterAsync();
        await f.Service.SettleAsync(1, OwnerAccountId, false);

        var confirmed = await f.Service.ConfirmAsync(1,
            new CheckoutConfirmDto { CheckoutDate = new DateTime(2026, 8, 12) }, OwnerAccountId, false);
        var json = Serialize(confirmed);
        Assert.Contains("已清算", json); // 响应层映射：DB 存"已通过"（CHECK 约束），对外契约终态"已清算"

        var room = await f.Context.Rooms.FindAsync(101);
        Assert.Equal(0, room!.Occupancy); // 释放床位

        // 清算通知（IT-C2-001 ⑥）：确认成功向学生投递一次
        Assert.Single(f.Notifications.Created);
        Assert.Equal("S001", f.Notifications.Created[0].StudentId);

        // 幂等：重复确认不报错、不重复释放（IT-C2-001 ③）、不重复通知
        var again = await f.Service.ConfirmAsync(1, new CheckoutConfirmDto(), OwnerAccountId, false);
        Assert.Contains("已清算", Serialize(again));
        room = await f.Context.Rooms.FindAsync(101);
        Assert.Equal(0, room!.Occupancy);
        Assert.Single(f.Notifications.Created);
    }

    [Fact]
    public async Task Confirm_WithoutSettle_Throws()
    {
        var f = new Fixture();
        await using var _ = f.Context;

        await f.RegisterAsync();
        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => f.Service.ConfirmAsync(1, new CheckoutConfirmDto(), OwnerAccountId, false));
        Assert.Contains("settle", ex.Message);
    }

    [Fact]
    public async Task Cancel_RevertsCheckoutDate_AndIsIdempotent()
    {
        var f = new Fixture();
        await using var _ = f.Context;

        await f.RegisterAsync();
        await f.Service.SettleAsync(1, OwnerAccountId, false); // 写入 CheckOut_Date

        var cancelled = await f.Service.CancelAsync(1, OwnerAccountId, false);
        var json = Serialize(cancelled);
        Assert.Contains("已取消", json);

        // 床位恢复在住（IT-C2-005 ②）
        var alloc = await f.Context.BedAllocations.FindAsync(1L);
        Assert.Null(alloc!.CheckOutDate);

        // 审计留痕（IT-C2-005 ③）：经审计公共服务（IAuditService）写入 D_Audit_Event，
        // 不直接落表（跨模块架构红线）。actor 为调用者账户。
        var audit = Assert.Single(f.Context.AuditEvents);
        Assert.Equal("CHECKOUT_CANCEL", audit.EventType);
        Assert.Equal("D_CHECKOUT_LOG", audit.TargetType);
        Assert.Equal("1", audit.TargetId);
        Assert.Equal(OwnerAccountId, audit.ActorAccountId);
        Assert.Contains("S001", audit.Details);

        // 幂等取消
        var again = await f.Service.CancelAsync(1, OwnerAccountId, false);
        Assert.Contains("已取消", Serialize(again));
        Assert.Single(f.Context.AuditEvents); // 幂等重放不重复留痕

        // 已取消后不可确认
        await Assert.ThrowsAsync<BusinessException>(
            () => f.Service.ConfirmAsync(1, new CheckoutConfirmDto(), OwnerAccountId, false));
    }

    [Fact]
    public async Task Get_ReturnsSummaryWithAllocationSnapshot()
    {
        var f = new Fixture();
        await using var _ = f.Context;

        await f.RegisterAsync();
        var summary = await f.Service.GetAsync(1, OwnerAccountId, false);
        var json = Serialize(summary);
        Assert.Contains("待清算", json);
        Assert.Contains("S001", json);
    }

    // ==================== 归属校验（评审整改：学生仅本人 / 宿管放行） ====================

    [Fact]
    public async Task Register_OtherStudentsAllocation_Throws403()
    {
        var f = new Fixture();
        await using var _ = f.Context;
        f.Context.UserAccounts.Add(new UserAccount
        {
            AccountId = 102,
            LoginName = "stu-102",
            PasswordHash = "h",
            AccountStatus = "正常",
            StudentId = "S002"
        });
        await f.Context.SaveChangesAsync();

        // 学生 B（账户 102）登记学生 A（S001）的分配 → 403
        await AssertForbidden(() => f.Service.RegisterAsync(1, new CheckoutRegisterDto(), 102, false));

        // 拦截生效：未产生清算记录
        Assert.False(await f.Context.CheckoutLogs.AnyAsync());
    }

    [Fact]
    public async Task OtherStudent_CannotGetSettleConfirmOrCancel()
    {
        var f = new Fixture();
        await using var _ = f.Context;
        f.Context.UserAccounts.Add(new UserAccount
        {
            AccountId = 102,
            LoginName = "stu-102",
            PasswordHash = "h",
            AccountStatus = "正常",
            StudentId = "S002"
        });
        await f.Context.SaveChangesAsync();

        await f.RegisterAsync(); // 本人（S001）登记

        await AssertForbidden(() => f.Service.GetAsync(1, 102, false));
        await AssertForbidden(() => f.Service.SettleAsync(1, 102, false));
        await AssertForbidden(() => f.Service.ConfirmAsync(1, new CheckoutConfirmDto(), 102, false));
        await AssertForbidden(() => f.Service.CancelAsync(1, 102, false));

        // 状态未被越权操作扰动
        var log = await f.Context.CheckoutLogs.FindAsync(1);
        Assert.Equal("待清算", log!.Status);
    }

    [Fact]
    public async Task DormAdmin_CanOperateOthersCheckout()
    {
        var f = new Fixture();
        await using var _ = f.Context;

        // 宿管无学生身份：accountId 为 null + isDormAdmin=true 即可代办登记/查询
        var created = await f.RegisterAsync(accountId: null, isDormAdmin: true);
        Assert.Contains("待清算", Serialize(created));

        var summary = await f.Service.GetAsync(1, null, true);
        Assert.Contains("S001", Serialize(summary));
    }

    // ==================== 通知失败不阻断（评审整改 S1） ====================

    [Fact]
    public async Task Confirm_NotificationNonBusinessFailure_StillSucceeds()
    {
        var f = new Fixture();
        await using var _ = f.Context;
        f.Notifications.ThrowNonBusiness = true; // 通知服务抛非业务异常

        await f.RegisterAsync();
        await f.Service.SettleAsync(1, OwnerAccountId, false);

        // 退宿状态已提交，接口不得 500（重试幂等会丢通知）——对齐 SlaDispatchService 升级通知口径
        var confirmed = await f.Service.ConfirmAsync(1,
            new CheckoutConfirmDto { CheckoutDate = new DateTime(2026, 8, 12) }, OwnerAccountId, false);

        Assert.Contains("已清算", Serialize(confirmed));
        var room = await f.Context.Rooms.FindAsync(101);
        Assert.Equal(0, room!.Occupancy); // 床位已释放
    }
}
