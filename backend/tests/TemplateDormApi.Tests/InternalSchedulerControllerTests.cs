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
    public async Task Deduction_PassesAttemptNoAndYearMonth()
    {
        var billing = new FakeBillingService();
        var controller = CreateController(new FakeCreditService(), billing);

        var actionResult = await controller.Deduction(attemptNo: 3, yearMonth: "2026-07");

        Assert.IsType<OkObjectResult>(actionResult);
        Assert.Equal(3, billing.LastAttemptNo);
        Assert.Equal("2026-07", billing.LastYearMonth);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(4)]
    public async Task Deduction_RejectsAttemptNoOutOfRange(int attemptNo)
    {
        var billing = new FakeBillingService();
        var controller = CreateController(new FakeCreditService(), billing);

        var actionResult = await controller.Deduction(attemptNo: attemptNo, yearMonth: "2026-07");

        var badRequest = Assert.IsType<BadRequestObjectResult>(actionResult);
        Assert.Equal(400, GetApiCode(badRequest.Value));
        Assert.False(billing.AutoDeductCalled);
    }

    [Theory]
    [InlineData("2026-7")]
    [InlineData("2026-13")]
    [InlineData("abc")]
    public async Task Deduction_RejectsInvalidYearMonth(string yearMonth)
    {
        var billing = new FakeBillingService();
        var controller = CreateController(new FakeCreditService(), billing);

        var actionResult = await controller.Deduction(attemptNo: 1, yearMonth: yearMonth);

        Assert.IsType<BadRequestObjectResult>(actionResult);
        Assert.False(billing.AutoDeductCalled);
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
        IBillingService billingService)
    {
        return new InternalSchedulerController(
            creditService,
            billingService,
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
}
