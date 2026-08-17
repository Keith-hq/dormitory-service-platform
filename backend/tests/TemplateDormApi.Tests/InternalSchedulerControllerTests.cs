using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using TemplateDormApi.Controllers;
using TemplateDormApi.DTO;
using TemplateDormApi.Models;
using TemplateDormApi.Security;
using TemplateDormApi.Services;

namespace TemplateDormApi.Tests;

public class InternalSchedulerControllerTests
{
    [Fact]
    public async Task CreditReset_UsesCurrentYearMonth()
    {
        var service = new FakeCreditService();
        var controller = CreateController(service, new FakeBillingService());
        var expected = DateTime.Now;

        await controller.CreditReset(CancellationToken.None);

        Assert.Equal(expected.Year, service.Year);
        Assert.Equal(expected.Month, service.Month);
    }

    [Fact]
    public async Task CreditReset_ReturnsOkWithResetResult()
    {
        var expected = new ResetResultDto
        {
            Processed = 1,
            Skipped = 0,
            Failed = 0
        };
        var service = new FakeCreditService { Result = expected };
        var controller = CreateController(service, new FakeBillingService());

        var actionResult = await controller.CreditReset(CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(actionResult);
        var response = Assert.IsType<ApiResponse<ResetResultDto>>(okResult.Value);
        Assert.Same(expected, response.Data);
    }

    // ==================== POST deduction（IT-C3-003） ====================

    [Fact]
    public async Task Deduction_DefaultsToCurrentMonth_AndAttemptNo1()
    {
        var billing = new FakeBillingService();
        var controller = CreateController(new FakeCreditService(), billing);

        var actionResult = await controller.Deduction();

        var okResult = Assert.IsType<OkObjectResult>(actionResult);
        Assert.Equal(200, GetApiCode(okResult.Value));
        Assert.Equal(1, billing.LastAttemptNo);
        Assert.Equal(DateTime.Now.ToString("yyyy-MM"), billing.LastYearMonth);
    }

    [Fact]
    public void Deduction_HasNoPublicParameters()
    {
        var method = typeof(InternalSchedulerController).GetMethod(nameof(InternalSchedulerController.Deduction));

        Assert.NotNull(method);
        Assert.Empty(method.GetParameters());
    }

    // ==================== POST power-restore（IT-C7-003） ====================

    [Fact]
    public async Task PowerRestore_CallsBillingService()
    {
        var billing = new FakeBillingService();
        var controller = CreateController(new FakeCreditService(), billing);

        var actionResult = await controller.PowerRestore();

        var okResult = Assert.IsType<OkObjectResult>(actionResult);
        Assert.Equal(200, GetApiCode(okResult.Value));
        Assert.True(billing.RestorePowerCalled);
    }

    [Fact]
    public async Task VisitorExpire_CallsVisitorService()
    {
        var visitor = new FakeVisitorService();
        var controller = CreateController(new FakeCreditService(), new FakeBillingService(), visitor);

        var actionResult = await controller.VisitorExpire();

        var okResult = Assert.IsType<OkObjectResult>(actionResult);
        Assert.Equal(200, GetApiCode(okResult.Value));
        Assert.True(visitor.ExpireCalled);
    }

    [Fact]
    public async Task VisitorExpire_ReturnsProcessedCount()
    {
        var visitor = new FakeVisitorService { ExpiredCount = 3 };
        var controller = CreateController(new FakeCreditService(), new FakeBillingService(), visitor);

        var actionResult = await controller.VisitorExpire();

        var okResult = Assert.IsType<OkObjectResult>(actionResult);
        var code = okResult.Value!.GetType().GetProperty("Code")!.GetValue(okResult.Value);
        Assert.Equal(200, (int)code!);
        var data = okResult.Value!.GetType().GetProperty("Data")!.GetValue(okResult.Value);
        var expiredCount = data!.GetType().GetProperty("expiredCount")!.GetValue(data);
        Assert.Equal(3, (int)expiredCount!);
    }

    [Fact]
    public void Controller_HasServiceKeyAuthAttribute()
    {
        Assert.NotNull(
            typeof(InternalSchedulerController)
                .GetCustomAttributes(typeof(ServiceKeyAuthAttribute), true)
                .SingleOrDefault());
    }

    private static int GetApiCode(object? value)
        => (int)(value!.GetType().GetProperty("Code")!.GetValue(value) ?? 0);

    private static InternalSchedulerController CreateController(
        ICreditService creditService,
        IBillingService billingService,
        IVisitorService? visitorService = null)
    {
        return new InternalSchedulerController(
            creditService,
            billingService,
            visitorService ?? new FakeVisitorService(),
            NullLogger<InternalSchedulerController>.Instance);
    }

    private sealed class FakeBillingService : IBillingService
    {
        public int? LastAttemptNo { get; private set; }
        public string? LastYearMonth { get; private set; }
        public bool AutoDeductCalled { get; private set; }
        public bool RestorePowerCalled { get; private set; }

        public Task AutoDeduct(int attemptNo, string yearMonth)
        {
            AutoDeductCalled = true;
            LastAttemptNo = attemptNo;
            LastYearMonth = yearMonth;
            return Task.CompletedTask;
        }

        public Task CheckPowerCut(string yearMonth) => Task.CompletedTask;

        public Task RestorePower()
        {
            RestorePowerCalled = true;
            return Task.CompletedTask;
        }

        public Task<decimal> GetBalance(string studentId) => Task.FromResult(0m);

        public Task<List<WalletLog>> GetWalletLogs(string studentId, string yearMonth)
            => Task.FromResult(new List<WalletLog>());

        public Task<string> GetPowerStatus(int roomId) => Task.FromResult("正常");
    }

    private sealed class FakeCreditService : ICreditService
    {
        public int? Year { get; private set; }
        public int? Month { get; private set; }
        public ResetResultDto Result { get; set; } = new();

        public Task<ResetResultDto> ResetMonthlyAsync(
            int year,
            int month,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Year = year;
            Month = month;
            return Task.FromResult(Result);
        }

        public Task<CreditResultDto> DeductAsync(
            CreditDeductDto dto,
            CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<CreditStatusDto> GetStatusAsync(
            string studentId,
            CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<CreditViewDto> GetViewAsync(
            string studentId,
            int accountId,
            CancellationToken cancellationToken)
            => throw new NotSupportedException();
    }

    private sealed class FakeVisitorService : IVisitorService
    {
        public bool ExpireCalled { get; private set; }
        public int ExpiredCount { get; set; }

        public Task<int> ExpireAsync()
        {
            ExpireCalled = true;
            return Task.FromResult(ExpiredCount);
        }

        public Task<VisitorAuthorization> ApplyAsync(string studentId, VisitorApplyRequest dto)
            => throw new NotSupportedException();

        public Task<PagedResult<VisitorAuthorization>> GetMyListAsync(string studentId, int page, int pageSize)
            => throw new NotSupportedException();

        public Task<VisitorAuthorization> GetCredentialAsync(int authId, string currentStudentId)
            => throw new NotSupportedException();

        public Task<VisitorAuthorization> RevokeAsync(int authId, string currentStudentId)
            => throw new NotSupportedException();
    }
}
