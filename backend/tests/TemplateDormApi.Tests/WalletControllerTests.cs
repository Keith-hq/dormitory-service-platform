using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using TemplateDormApi.Controllers;
using TemplateDormApi.Data;
using TemplateDormApi.DTO;
using TemplateDormApi.Models;
using TemplateDormApi.Services;

namespace TemplateDormApi.Tests;

/// <summary>
/// 钱包控制器测试（难点② STU-04/05/06/07）：
/// 1. 路由模板对齐契约（POST /api/wallet/payments、POST /api/wallet/recharges、GET /api/students/{id}/wallet、GET /api/students/{id}/fees）；
/// 2. 学生身份从 JWT 解析（非本人钱包/账单 403）；
/// 3. Idempotency-Key 缺失/超长入口拒绝；
/// 4. SP 结果码 → 中文消息映射。
/// </summary>
public class WalletControllerTests
{
    // ==================== 路由与鉴权挂载 ====================

    [Fact]
    public void Endpoints_MatchContractRoutes()
    {
        AssertRoute(nameof(WalletController.ManualPay), "wallet/payments");
        AssertRoute(nameof(WalletController.Recharge), "wallet/recharges");
        AssertRoute(nameof(WalletController.GetWallet), "students/{studentId}/wallet");
        AssertRoute(nameof(WalletController.GetStudentFees), "students/{studentId}/fees");
    }

    [Fact]
    public void Endpoints_RequireAuthentication()
    {
        // 四个端点均为 [Authorize]（无策略——学生身份由 ResolveStudentId 从 JWT 解析）
        Assert.NotNull(typeof(WalletController)
            .GetMethod(nameof(WalletController.ManualPay))!
            .GetCustomAttributes(typeof(AuthorizeAttribute), true)
            .SingleOrDefault());
        Assert.NotNull(typeof(WalletController)
            .GetMethod(nameof(WalletController.Recharge))!
            .GetCustomAttributes(typeof(AuthorizeAttribute), true)
            .SingleOrDefault());
        Assert.NotNull(typeof(WalletController)
            .GetMethod(nameof(WalletController.GetWallet))!
            .GetCustomAttributes(typeof(AuthorizeAttribute), true)
            .SingleOrDefault());
        Assert.NotNull(typeof(WalletController)
            .GetMethod(nameof(WalletController.GetStudentFees))!
            .GetCustomAttributes(typeof(AuthorizeAttribute), true)
            .SingleOrDefault());
    }

    // ==================== STU-05 人工缴费 ====================

    [Fact]
    public async Task ManualPay_Success_ForwardsDetailAndStudent()
    {
        var (context, studentId) = await CreateStudentContext(accountId: 101);
        var fake = new FakeWalletService { ManualPayResult = 0 };
        var controller = CreateController(fake, context);
        SetUser(controller, "101");
        controller.Request.Headers["Idempotency-Key"] = "PAY-K-1";

        var result = await controller.ManualPay(new ManualPayRequest { DetailId = 42 });

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(200, GetApiCode(ok.Value));
        Assert.Equal(42, fake.LastDetailId);
        Assert.Equal(studentId, fake.LastStudentId);
        Assert.Equal("PAY-K-1", fake.LastIdempotencyKey);
    }

    [Fact]
    public async Task ManualPay_MissingIdempotencyKey_Returns400()
    {
        var (context, _) = await CreateStudentContext(accountId: 102);
        var fake = new FakeWalletService { ManualPayResult = 0 };
        var controller = CreateController(fake, context);
        SetUser(controller, "102");

        var result = await controller.ManualPay(new ManualPayRequest { DetailId = 1 });

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(400, GetApiCode(ok.Value));
        Assert.False(fake.ManualPayCalled);
    }

    [Fact]
    public async Task ManualPay_KeyTooLong_Returns400()
    {
        var (context, _) = await CreateStudentContext(accountId: 103);
        var fake = new FakeWalletService { ManualPayResult = 0 };
        var controller = CreateController(fake, context);
        SetUser(controller, "103");
        controller.Request.Headers["Idempotency-Key"] = new string('k', 101);

        var result = await controller.ManualPay(new ManualPayRequest { DetailId = 1 });

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(400, GetApiCode(ok.Value));
        Assert.False(fake.ManualPayCalled);
    }

    [Theory]
    [InlineData(1, "明细不存在")]
    [InlineData(2, "明细非本人，无法缴费")]
    [InlineData(3, "钱包余额不足")]
    [InlineData(4, "账单已缴或无需缴费")]
    [InlineData(5, "Idempotency-Key 已被其它交易占用")]
    public async Task ManualPay_ResultCode_MapsToChineseMessage(int rc, string expectedMessage)
    {
        var (context, _) = await CreateStudentContext(accountId: 104);
        var fake = new FakeWalletService { ManualPayResult = rc };
        var controller = CreateController(fake, context);
        SetUser(controller, "104");
        controller.Request.Headers["Idempotency-Key"] = "PAY-K-2";

        var result = await controller.ManualPay(new ManualPayRequest { DetailId = 1 });

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(400, GetApiCode(ok.Value));
        Assert.Equal(expectedMessage, GetApiMessage(ok.Value));
    }

    // ==================== STU-07 充值 ====================

    [Fact]
    public async Task Recharge_Success_ForwardsAmountAndStudent()
    {
        var (context, studentId) = await CreateStudentContext(accountId: 105);
        var fake = new FakeWalletService { RechargeResult = 0 };
        var controller = CreateController(fake, context);
        SetUser(controller, "105");
        controller.Request.Headers["Idempotency-Key"] = "RC-K-1";

        var result = await controller.Recharge(new RechargeRequest { Amount = 88.5m });

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(200, GetApiCode(ok.Value));
        Assert.Equal(88.5m, fake.LastAmount);
        Assert.Equal(studentId, fake.LastStudentId);
        Assert.Equal("RC-K-1", fake.LastIdempotencyKey);
    }

    [Fact]
    public async Task Recharge_MissingIdempotencyKey_Returns400()
    {
        var (context, _) = await CreateStudentContext(accountId: 106);
        var fake = new FakeWalletService { RechargeResult = 0 };
        var controller = CreateController(fake, context);
        SetUser(controller, "106");

        var result = await controller.Recharge(new RechargeRequest { Amount = 10m });

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(400, GetApiCode(ok.Value));
        Assert.False(fake.RechargeCalled);
    }

    [Fact]
    public async Task Recharge_InvalidAmount_Returns400()
    {
        var (context, _) = await CreateStudentContext(accountId: 107);
        var fake = new FakeWalletService { RechargeResult = 1 };
        var controller = CreateController(fake, context);
        SetUser(controller, "107");
        controller.Request.Headers["Idempotency-Key"] = "RC-K-2";

        var result = await controller.Recharge(new RechargeRequest { Amount = 0m });

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(400, GetApiCode(ok.Value));
        Assert.Equal("充值金额必须大于 0", GetApiMessage(ok.Value));
    }

    // ==================== STU-06 钱包查询 ====================

    [Fact]
    public async Task GetWallet_OwnWallet_ReturnsBalanceAndLogs()
    {
        var (context, studentId) = await CreateStudentContext(accountId: 108);
        var fake = new FakeWalletService
        {
            Wallet = new WalletViewDto
            {
                StudentId = studentId,
                Balance = 950m,
                Logs = new List<WalletLogDto> { new() { LogId = 1, Amount = 150m, TransactionType = "人工缴费" } }
            }
        };
        var controller = CreateController(fake, context);
        SetUser(controller, "108");

        var result = await controller.GetWallet(studentId, "2026-07");

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse<WalletViewDto>>(ok.Value);
        Assert.Equal(200, response.Code);
        Assert.Equal(950m, response.Data!.Balance);
        Assert.Single(response.Data.Logs);
        Assert.Equal("2026-07", fake.LastWalletYearMonth);
    }

    [Fact]
    public async Task GetWallet_OtherStudentsWallet_Returns403()
    {
        var (context, studentId) = await CreateStudentContext(accountId: 109);
        var fake = new FakeWalletService();
        var controller = CreateController(fake, context);
        SetUser(controller, "109");

        var result = await controller.GetWallet(studentId + "-OTHER");

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(403, GetApiCode(ok.Value));
        Assert.False(fake.GetWalletCalled);
    }

    [Theory]
    [InlineData("2026-7")]
    [InlineData("bad")]
    public async Task GetWallet_InvalidYearMonth_Returns400(string yearMonth)
    {
        var (context, studentId) = await CreateStudentContext(accountId: 110);
        var fake = new FakeWalletService();
        var controller = CreateController(fake, context);
        SetUser(controller, "110");

        var result = await controller.GetWallet(studentId, yearMonth);

        Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.False(fake.GetWalletCalled);
    }

    // ==================== STU-04 学生账单查询 ====================

    [Fact]
    public async Task GetStudentFees_OwnStudent_ReturnsFees()
    {
        var (context, studentId) = await CreateStudentContext(accountId: 111);
        var fake = new FakeWalletService
        {
            Fees = new StudentFeesDto { StudentId = studentId, Count = 1 }
        };
        var controller = CreateController(fake, context);
        SetUser(controller, "111");

        var result = await controller.GetStudentFees(studentId, "2026-07");

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse<StudentFeesDto>>(ok.Value);
        Assert.Equal(200, response.Code);
        Assert.Equal(studentId, response.Data!.StudentId);
        Assert.Equal("2026-07", fake.LastFeesYearMonth);
    }

    [Fact]
    public async Task GetStudentFees_NoYearMonth_ForwardsNull()
    {
        var (context, studentId) = await CreateStudentContext(accountId: 112);
        var fake = new FakeWalletService { Fees = new StudentFeesDto() };
        var controller = CreateController(fake, context);
        SetUser(controller, "112");

        var result = await controller.GetStudentFees(studentId);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(200, GetApiCode(ok.Value));
        Assert.True(fake.GetStudentFeesCalled);
        Assert.Null(fake.LastFeesYearMonth);
    }

    [Fact]
    public async Task GetStudentFees_OtherStudentsFees_Returns403()
    {
        var (context, studentId) = await CreateStudentContext(accountId: 113);
        var fake = new FakeWalletService();
        var controller = CreateController(fake, context);
        SetUser(controller, "113");

        var result = await controller.GetStudentFees(studentId + "-OTHER");

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(403, GetApiCode(ok.Value));
        Assert.False(fake.GetStudentFeesCalled);
    }

    [Theory]
    [InlineData("2026-7")]
    [InlineData("bad")]
    public async Task GetStudentFees_InvalidYearMonth_Returns400(string yearMonth)
    {
        var (context, studentId) = await CreateStudentContext(accountId: 114);
        var fake = new FakeWalletService();
        var controller = CreateController(fake, context);
        SetUser(controller, "114");

        var result = await controller.GetStudentFees(studentId, yearMonth);

        Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.False(fake.GetStudentFeesCalled);
    }

    // ==================== 辅助 ====================

    /// <summary>建一个账户与学生关联的上下文，返回 (context, studentId)</summary>
    private static async Task<(AppDbContext, string)> CreateStudentContext(int accountId)
    {
        var context = TestDbContextFactory.Create();
        var studentId = $"S-T{accountId}";
        context.UserAccounts.Add(new UserAccount
        {
            AccountId = accountId, LoginName = $"stu-{accountId}", PasswordHash = "h",
            AccountStatus = "正常", StudentId = studentId
        });
        await context.SaveChangesAsync();
        return (context, studentId);
    }

    private static int GetApiCode(object? value)
        => (int)(value!.GetType().GetProperty("Code")!.GetValue(value) ?? 0);

    private static string GetApiMessage(object? value)
        => (string)(value!.GetType().GetProperty("Message")!.GetValue(value) ?? string.Empty);

    private static void AssertRoute(string methodName, string expectedTemplate)
    {
        var attribute = typeof(WalletController)
            .GetMethod(methodName)!
            .GetCustomAttributes(true)
            .OfType<HttpMethodAttribute>()
            .Single();

        Assert.Equal(expectedTemplate, attribute.Template);
    }

    private static WalletController CreateController(FakeWalletService service, AppDbContext context)
        => new(service, context);

    private static void SetUser(ControllerBase controller, string accountId)
    {
        var identity = new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, accountId)
        }, "test");
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
        };
    }

    private sealed class FakeWalletService : IWalletService
    {
        public int ManualPayResult { get; set; }
        public int RechargeResult { get; set; }
        public WalletViewDto? Wallet { get; set; }
        public StudentFeesDto? Fees { get; set; }

        public bool ManualPayCalled { get; private set; }
        public bool RechargeCalled { get; private set; }
        public bool GetWalletCalled { get; private set; }
        public bool GetStudentFeesCalled { get; private set; }

        public int? LastDetailId { get; private set; }
        public string? LastStudentId { get; private set; }
        public string? LastIdempotencyKey { get; private set; }
        public decimal? LastAmount { get; private set; }
        public string? LastWalletYearMonth { get; private set; }
        public string? LastFeesYearMonth { get; private set; }

        public Task<int> ManualPay(int detailId, string studentId, string idempotencyKey)
        {
            ManualPayCalled = true;
            LastDetailId = detailId;
            LastStudentId = studentId;
            LastIdempotencyKey = idempotencyKey;
            return Task.FromResult(ManualPayResult);
        }

        public Task<int> Recharge(string studentId, decimal amount, string idempotencyKey)
        {
            RechargeCalled = true;
            LastStudentId = studentId;
            LastAmount = amount;
            LastIdempotencyKey = idempotencyKey;
            return Task.FromResult(RechargeResult);
        }

        public Task<WalletViewDto> GetWallet(string studentId, string yearMonth)
        {
            GetWalletCalled = true;
            LastWalletYearMonth = yearMonth;
            return Task.FromResult(Wallet ?? new WalletViewDto { StudentId = studentId });
        }

        public Task<StudentFeesDto> GetStudentFees(string studentId, string? yearMonth)
        {
            GetStudentFeesCalled = true;
            LastFeesYearMonth = yearMonth;
            return Task.FromResult(Fees ?? new StudentFeesDto { StudentId = studentId });
        }
    }
}
