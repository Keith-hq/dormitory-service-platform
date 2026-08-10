using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using TemplateDormApi.Controllers;
using TemplateDormApi.DTO;
using TemplateDormApi.Security;
using TemplateDormApi.Services;

namespace TemplateDormApi.Tests;

public class InternalSchedulerControllerTests
{
    [Fact]
    public async Task CreditReset_UsesCurrentYearMonth()
    {
        var service = new FakeCreditService();
        var controller = CreateController(service);
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
        var controller = CreateController(service);

        var actionResult = await controller.CreditReset(CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(actionResult);
        var response = Assert.IsType<ApiResponse<ResetResultDto>>(okResult.Value);
        Assert.Same(expected, response.Data);
    }

    [Fact]
    public void Controller_HasServiceKeyAuthAttribute()
    {
        Assert.NotNull(
            typeof(InternalSchedulerController)
                .GetCustomAttributes(typeof(ServiceKeyAuthAttribute), true)
                .SingleOrDefault());
    }

    private static InternalSchedulerController CreateController(ICreditService creditService)
    {
        return new InternalSchedulerController(
            creditService,
            NullLogger<InternalSchedulerController>.Instance);
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
