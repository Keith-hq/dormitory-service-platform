using Microsoft.Extensions.Logging.Abstractions;
using TemplateDormApi.DTO;
using TemplateDormApi.Jobs;
using TemplateDormApi.Services;

namespace TemplateDormApi.Tests;

public class CreditResetJobTests
{
    [Fact]
    public async Task ExecuteAsync_ResetsCurrentYearMonth()
    {
        var service = new FakeCreditService();
        var job = CreateJob(service);
        var expected = DateTime.Now;

        await job.ExecuteAsync(CancellationToken.None);

        Assert.Equal(expected.Year, service.Year);
        Assert.Equal(expected.Month, service.Month);
    }

    [Fact]
    public async Task ExecuteAsync_PropagatesCancellation()
    {
        var service = new FakeCreditService();
        var job = CreateJob(service);
        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => job.ExecuteAsync(cancellationTokenSource.Token));
    }

    [Fact]
    public async Task ExecuteAsync_PartialFailureDoesNotThrow()
    {
        var service = new FakeCreditService
        {
            Result = new ResetResultDto { Failed = 3 }
        };
        var job = CreateJob(service);

        await job.ExecuteAsync(CancellationToken.None);
    }

    private static CreditResetJob CreateJob(ICreditService creditService)
    {
        return new CreditResetJob(
            creditService,
            NullLogger<CreditResetJob>.Instance);
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
