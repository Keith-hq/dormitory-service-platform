using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Logging.Abstractions;
using TemplateDormApi.Data;
using TemplateDormApi.DTO;
using TemplateDormApi.Exceptions;
using TemplateDormApi.Models;
using TemplateDormApi.Repository;
using TemplateDormApi.Services;

namespace TemplateDormApi.Tests;

/// <summary>
/// 信用分申诉服务（APPEAL-01/02/03）核心业务链路：
/// 提交（仅扣分明细）/ 重复申诉拦截 / 本人与宿管鉴权 / 复核通过恢复分值（Math.Abs）/ 复核人 Admin_ID 绑定。
/// </summary>
public class CreditAppealServiceTests
{
    private const string StudentId = "20260001";
    private const int StudentAccountId = 101;
    private const string AdminId = "A-01";
    private const int AdminAccountId = 201;

    private static CreditAppealService CreateService(
        AppDbContext context,
        FakeCreditService creditService,
        FakeNotificationService notificationService,
        FakeAuditService auditService)
    {
        return new CreditAppealService(
            context,
            new UserAccountRepository(context),
            creditService,
            notificationService,
            auditService,
            NullLogger<CreditAppealService>.Instance);
    }

    // 041：D_Credit_Appeal 不再冗余 Student_ID；学生归属由 CreditLogId 关联的
    // D_Credit_Log.Student_ID 决定。各 appeal 初始化块随之去掉 StudentId 赋值。
    private static async Task<AppDbContext> SeedStudentContextAsync()
    {
        var context = TestDbContextFactory.Create();
        context.UserAccounts.Add(new UserAccount
        {
            AccountId = StudentAccountId,
            LoginName = "student-001",
            PasswordHash = "test-only",
            AccountStatus = "正常",
            StudentId = StudentId
        });
        context.CreditLogs.Add(new CreditLog
        {
            LogId = 1,
            StudentId = StudentId,
            ScoreChange = -5,
            Reason = "晚归扣分",
            EventKey = "EVT-DEDUCT-1",
            CreateTime = DateTime.Now
        });
        await context.SaveChangesAsync();
        return context;
    }

    private static async Task<AppDbContext> SeedAdminContextAsync()
    {
        var context = await SeedStudentContextAsync();
        context.UserAccounts.Add(new UserAccount
        {
            AccountId = AdminAccountId,
            LoginName = "admin-001",
            PasswordHash = "test-only",
            AccountStatus = "正常",
            AdminId = AdminId
        });
        await context.SaveChangesAsync();
        return context;
    }

    // ===== APPEAL-01 提交 =====

    [Fact]
    public async Task SubmitAsync_OwnDeductionLog_CreatesPendingAppeal()
    {
        await using var context = await SeedStudentContextAsync();
        var service = CreateService(context, new FakeCreditService(), new FakeNotificationService(), new FakeAuditService());

        var dto = new CreateCreditAppealRequest { CreditRecordId = 1, Reason = "该次晚归非本人所为" };
        var result = await service.SubmitAsync(StudentAccountId, dto, CancellationToken.None);

        Assert.True(result.AppealId > 0);
        Assert.Equal(StudentId, result.StudentId);
        Assert.Equal("待复核", result.Status);
        Assert.Equal(-5, result.ScoreChange);
    }

    [Fact]
    public async Task SubmitAsync_PositiveScoreLog_Throws400()
    {
        await using var context = await SeedStudentContextAsync();
        // 月度重置 +45 属正分流水，申诉通过会经 Math.Abs 二次加分 —— 提交侧必须拦截
        context.CreditLogs.Add(new CreditLog
        {
            LogId = 2,
            StudentId = StudentId,
            ScoreChange = 45,
            Reason = "月度重置",
            EventKey = "EVT-RESET-1",
            CreateTime = DateTime.Now
        });
        await context.SaveChangesAsync();

        var service = CreateService(context, new FakeCreditService(), new FakeNotificationService(), new FakeAuditService());
        var ex = await Assert.ThrowsAsync<BusinessException>(() =>
            service.SubmitAsync(StudentAccountId, new CreateCreditAppealRequest { CreditRecordId = 2, Reason = "重置不该加分" }, CancellationToken.None));

        Assert.Equal(400, ex.HttpStatus);
        Assert.Contains("扣分", ex.Message);
    }

    [Fact]
    public async Task SubmitAsync_DuplicateAppeal_Throws400()
    {
        await using var context = await SeedStudentContextAsync();
        context.CreditAppeals.Add(new CreditAppeal
        {
            CreditLogId = 1,
            Reason = "已申诉过",
            Status = "待复核",
            CreateTime = DateTime.Now
        });
        await context.SaveChangesAsync();

        var service = CreateService(context, new FakeCreditService(), new FakeNotificationService(), new FakeAuditService());
        var ex = await Assert.ThrowsAsync<BusinessException>(() =>
            service.SubmitAsync(StudentAccountId, new CreateCreditAppealRequest { CreditRecordId = 1, Reason = "重复" }, CancellationToken.None));

        Assert.Equal(400, ex.HttpStatus);
        Assert.Contains("已申诉过", ex.Message);
    }

    [Fact]
    public async Task SubmitAsync_OthersLog_Throws400()
    {
        await using var context = await SeedStudentContextAsync();
        context.CreditLogs.Add(new CreditLog
        {
            LogId = 2,
            StudentId = "20260002",
            ScoreChange = -3,
            Reason = "他人扣分",
            EventKey = "EVT-OTHER",
            CreateTime = DateTime.Now
        });
        await context.SaveChangesAsync();

        var service = CreateService(context, new FakeCreditService(), new FakeNotificationService(), new FakeAuditService());
        var ex = await Assert.ThrowsAsync<BusinessException>(() =>
            service.SubmitAsync(StudentAccountId, new CreateCreditAppealRequest { CreditRecordId = 2, Reason = "非本人明细" }, CancellationToken.None));

        Assert.Equal(400, ex.HttpStatus);
        Assert.Contains("不属于本人", ex.Message);
    }

    // ===== APPEAL-02 查询鉴权 =====

    [Fact]
    public async Task GetMyAsync_Self_ReturnsOwnAppeals()
    {
        await using var context = await SeedStudentContextAsync();
        context.CreditAppeals.Add(new CreditAppeal
        {
            CreditLogId = 1,
            Reason = "误扣",
            Status = "待复核",
            CreateTime = DateTime.Now
        });
        await context.SaveChangesAsync();

        var service = CreateService(context, new FakeCreditService(), new FakeNotificationService(), new FakeAuditService());
        var result = await service.GetMyAsync(StudentAccountId, StudentId, 1, 10, false, CancellationToken.None);

        Assert.Equal(1, result.Total);
        Assert.Equal("误扣", Assert.Single(result.Items).Reason);
    }

    [Fact]
    public async Task GetMyAsync_StudentQueryingOther_Throws403()
    {
        await using var context = await SeedStudentContextAsync();
        var service = CreateService(context, new FakeCreditService(), new FakeNotificationService(), new FakeAuditService());

        var ex = await Assert.ThrowsAsync<BusinessException>(() =>
            service.GetMyAsync(StudentAccountId, "20260002", 1, 10, false, CancellationToken.None));

        Assert.Equal(403, ex.HttpStatus);
    }

    [Fact]
    public async Task GetMyAsync_NonStudent_Throws403()
    {
        await using var context = await SeedStudentContextAsync();
        context.UserAccounts.Add(new UserAccount
        {
            AccountId = 301,
            LoginName = "repairman-001",
            PasswordHash = "test-only",
            AccountStatus = "正常"
        });
        await context.SaveChangesAsync();

        var service = CreateService(context, new FakeCreditService(), new FakeNotificationService(), new FakeAuditService());
        // 维修员等非学生账号不得查任意学生申诉（APPEAL-02 鉴权收紧）
        var ex = await Assert.ThrowsAsync<BusinessException>(() =>
            service.GetMyAsync(301, StudentId, 1, 10, false, CancellationToken.None));

        Assert.Equal(403, ex.HttpStatus);
    }

    [Fact]
    public async Task GetMyAsync_DormAdmin_CanQueryAny()
    {
        await using var context = await SeedStudentContextAsync();
        var service = CreateService(context, new FakeCreditService(), new FakeNotificationService(), new FakeAuditService());

        var result = await service.GetMyAsync(AdminAccountId, StudentId, 1, 10, true, CancellationToken.None);

        Assert.Equal(0, result.Total);
    }

    // ===== APPEAL-03 复核 =====

    [Fact]
    public async Task ReviewAsync_Approve_RestoresAbsScore_AndBindsAdminId()
    {
        await using var context = await SeedAdminContextAsync();
        context.CreditAppeals.Add(new CreditAppeal
        {
            AppealId = 1,
            CreditLogId = 1,
            Reason = "误扣",
            Status = "待复核",
            CreateTime = DateTime.Now
        });
        await context.SaveChangesAsync();

        var credit = new FakeCreditService();
        var service = CreateService(context, credit, new FakeNotificationService(), new FakeAuditService());

        var result = await service.ReviewAsync(
            appealId: 1,
            accountId: AdminAccountId,
            new ReviewCreditAppealRequest { Result = "通过", Note = "经核实确属误扣" },
            CancellationToken.None);

        Assert.Equal("已通过", result.Status);
        Assert.Equal(AdminId, result.ReviewedBy); // Reviewed_By 存 Admin_ID，非登录名
        Assert.NotNull(result.ReviewTime);
        // 恢复分值 = Math.Abs(ScoreChange) = 5
        var restore = Assert.Single(credit.Restores);
        Assert.Equal(StudentId, restore.StudentId);
        Assert.Equal(5, restore.Score);
        Assert.Equal("APPEAL-1", restore.EventKey);
    }

    [Fact]
    public async Task ReviewAsync_Approve_PersistsStatus_WhenRestoreClearsTracking()
    {
        // D3 回归：真实 RestoreAsync 的恢复路径会 ChangeTracker.Clear()，
        // 把 appeal 实体脱离跟踪；修复后必须重新加载 appeal，确保首次复核即落库。
        await using var context = await SeedAdminContextAsync();
        context.CreditAppeals.Add(new CreditAppeal
        {
            AppealId = 1,
            CreditLogId = 1,
            Reason = "误扣",
            Status = "待复核",
            CreateTime = DateTime.Now
        });
        await context.SaveChangesAsync();

        var credit = new FakeCreditService { ContextToClearOnRestore = context };
        var service = CreateService(context, credit, new FakeNotificationService(), new FakeAuditService());

        var result = await service.ReviewAsync(
            appealId: 1,
            accountId: AdminAccountId,
            new ReviewCreditAppealRequest { Result = "通过", Note = "经核实确属误扣" },
            CancellationToken.None);

        Assert.Equal("已通过", result.Status);
        // 关键断言：即使 RestoreAsync 清了 ChangeTracker，DB（InMemory）中状态也必须已更新
        var persisted = await context.CreditAppeals.FindAsync(1);
        Assert.NotNull(persisted);
        Assert.Equal("已通过", persisted!.Status);
        Assert.Equal(AdminId, persisted.ReviewedBy);
    }

    [Fact]
    public async Task ReviewAsync_Reject_NoRestore_AndMarksRejected()
    {
        await using var context = await SeedAdminContextAsync();
        context.CreditAppeals.Add(new CreditAppeal
        {
            AppealId = 1,
            CreditLogId = 1,
            Reason = "误扣",
            Status = "待复核",
            CreateTime = DateTime.Now
        });
        await context.SaveChangesAsync();

        var credit = new FakeCreditService();
        var service = CreateService(context, credit, new FakeNotificationService(), new FakeAuditService());

        var result = await service.ReviewAsync(
            appealId: 1,
            accountId: AdminAccountId,
            new ReviewCreditAppealRequest { Result = "驳回", Note = "证据不足" },
            CancellationToken.None);

        Assert.Equal("已驳回", result.Status);
        Assert.Equal("证据不足", result.ResultDesc);
        Assert.Equal(AdminId, result.ReviewedBy);
        Assert.Empty(credit.Restores);
    }

    [Fact]
    public async Task ReviewAsync_AccountWithoutAdminId_Throws403()
    {
        await using var context = await SeedAdminContextAsync();
        // 复核账号（DormAdmin 角色）未关联 Admin_ID —— 解析不到复核人，阻止落库
        context.UserAccounts.Add(new UserAccount
        {
            AccountId = 401,
            LoginName = "admin-nolink",
            PasswordHash = "test-only",
            AccountStatus = "正常"
        });
        context.CreditAppeals.Add(new CreditAppeal
        {
            AppealId = 1,
            CreditLogId = 1,
            Reason = "误扣",
            Status = "待复核",
            CreateTime = DateTime.Now
        });
        await context.SaveChangesAsync();

        var service = CreateService(context, new FakeCreditService(), new FakeNotificationService(), new FakeAuditService());
        var ex = await Assert.ThrowsAsync<BusinessException>(() =>
            service.ReviewAsync(1, 401, new ReviewCreditAppealRequest { Result = "通过", Note = "x" }, CancellationToken.None));

        Assert.Equal(403, ex.HttpStatus);
    }

    [Fact]
    public async Task ReviewAsync_NotFound_Throws404()
    {
        await using var context = await SeedAdminContextAsync();
        var service = CreateService(context, new FakeCreditService(), new FakeNotificationService(), new FakeAuditService());

        var ex = await Assert.ThrowsAsync<BusinessException>(() =>
            service.ReviewAsync(999, AdminAccountId, new ReviewCreditAppealRequest { Result = "通过" }, CancellationToken.None));

        Assert.Equal(404, ex.HttpStatus);
    }

    [Fact]
    public async Task ReviewAsync_AlreadyReviewed_Throws400()
    {
        await using var context = await SeedAdminContextAsync();
        context.CreditAppeals.Add(new CreditAppeal
        {
            AppealId = 1,
            CreditLogId = 1,
            Reason = "误扣",
            Status = "已通过",
            ReviewedBy = AdminId,
            ReviewTime = DateTime.Now,
            CreateTime = DateTime.Now
        });
        await context.SaveChangesAsync();

        var service = CreateService(context, new FakeCreditService(), new FakeNotificationService(), new FakeAuditService());
        var ex = await Assert.ThrowsAsync<BusinessException>(() =>
            service.ReviewAsync(1, AdminAccountId, new ReviewCreditAppealRequest { Result = "通过" }, CancellationToken.None));

        Assert.Equal(400, ex.HttpStatus);
        Assert.Contains("已复核", ex.Message);
    }

    [Fact]
    public void ReviewRequest_RejectWithoutNote_IsInvalid()
    {
        var dto = new ReviewCreditAppealRequest { Result = "驳回" };
        var results = new List<ValidationResult>();

        Validator.TryValidateObject(dto, new ValidationContext(dto), results, validateAllProperties: true);

        Assert.Contains(results, r => r.MemberNames.Contains(nameof(ReviewCreditAppealRequest.Note)));
    }

    [Fact]
    public void ReviewRequest_ApproveWithoutNote_IsValid()
    {
        var dto = new ReviewCreditAppealRequest { Result = "通过" };
        var results = new List<ValidationResult>();

        Validator.TryValidateObject(dto, new ValidationContext(dto), results, validateAllProperties: true);

        Assert.DoesNotContain(results, r => r.MemberNames.Contains(nameof(ReviewCreditAppealRequest.Note)));
    }

    // ===== Fakes =====

    private sealed class FakeCreditService : ICreditService
    {
        public List<(string StudentId, int Score, string EventKey, string Reason)> Restores { get; } = new();

        /// <summary>模拟真实 CreditRepository.ClearTracking() 对 DbContext 全量清跟踪的行为。</summary>
        public AppDbContext? ContextToClearOnRestore { get; set; }

        public Task<CreditResultDto> RestoreAsync(
            string studentId,
            int restoreScore,
            string eventKey,
            string reason,
            CancellationToken cancellationToken)
        {
            ContextToClearOnRestore?.ChangeTracker.Clear();
            Restores.Add((studentId, restoreScore, eventKey, reason));
            return Task.FromResult(new CreditResultDto { StudentId = studentId, CurrentScore = restoreScore });
        }

        public Task<ResetResultDto> ResetMonthlyAsync(int year, int month, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<CreditResultDto> DeductAsync(CreditDeductDto dto, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<CreditStatusDto> GetStatusAsync(string studentId, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<CreditViewDto> GetViewAsync(string studentId, int accountId, CancellationToken cancellationToken)
            => throw new NotSupportedException();
    }

    private sealed class FakeNotificationService : INotificationService
    {
        public Task<PagedResult<NotificationItemDto>> GetPagedAsync(
            int recipientAccountId, int page, int pageSize, string? isRead)
            => throw new NotSupportedException();

        public Task MarkReadAsync(int notificationId, int recipientAccountId)
            => throw new NotSupportedException();

        public Task MarkBatchReadAsync(IReadOnlyCollection<int> notificationIds, int recipientAccountId)
            => throw new NotSupportedException();

        public Task<UnreadCountDto> GetUnreadCountAsync(int recipientAccountId)
            => throw new NotSupportedException();

        public Task<NotificationItemDto> CreateAsync(NotificationCreateDto dto)
            => Task.FromResult(new NotificationItemDto());
    }

    private sealed class FakeAuditService : IAuditService
    {
        public Task LogEventAsync(
            string eventType, string? targetType = null, string? targetId = null,
            int? actorAccountId = null, DateTime? eventTime = null, string? details = null)
            => Task.CompletedTask;
    }
}
