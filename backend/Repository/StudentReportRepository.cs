using Microsoft.EntityFrameworkCore;
using TemplateDormApi.Data;
using TemplateDormApi.DTO;

namespace TemplateDormApi.Repository;

public sealed class StudentReportRepository : FrameworkRepositoryBase
{
    public StudentReportRepository(AppDbContext context) : base(context) { }

    public async Task<MonthlyFeeReportDto> GetMonthlyFeeAsync(
        string studentId,
        MonthlyReportQueryDto query,
        CancellationToken cancellationToken)
    {
        var yearMonth = ResolveYearMonth(query.YearMonth);
        var details = from detail in DbContext.FeeDetails.AsNoTracking()
                      join fee in DbContext.UtilityFees.AsNoTracking()
                          on (long)detail.FeeId equals fee.FeeId
                      where detail.StudentId == studentId &&
                            detail.BillType == "月度" &&
                            fee.YearMonth == yearMonth
                      select new
                      {
                          Amount = detail.WaterShare + detail.PowerShare,
                          detail.IsPaid
                      };

        return new MonthlyFeeReportDto
        {
            YearMonth = yearMonth,
            UtilityTotal = await details.SumAsync(item => (decimal?)item.Amount, cancellationToken) ?? 0m,
            PaidTotal = await details.Where(item => item.IsPaid == "是")
                .SumAsync(item => (decimal?)item.Amount, cancellationToken) ?? 0m
        };
    }

    public async Task<FacilityUsageReportDto> GetFacilityUsageAsync(
        string studentId,
        MonthlyReportQueryDto query,
        CancellationToken cancellationToken)
    {
        var yearMonth = ResolveYearMonth(query.YearMonth);
        var month = DateTime.ParseExact(yearMonth, "yyyy-MM", System.Globalization.CultureInfo.InvariantCulture);
        var nextMonth = month.AddMonths(1);
        var count = await DbContext.FacilityBookings.AsNoTracking()
            .CountAsync(item =>
                item.StudentId == studentId &&
                item.StartTime >= month &&
                item.StartTime < nextMonth &&
                (item.Status == "使用中" || item.Status == "已完成"),
                cancellationToken);

        return new FacilityUsageReportDto { YearMonth = yearMonth, UsageCount = count };
    }

    public Task<AnnualReportDto> GetAnnualReportAsync(
        string studentId,
        AnnualReportQueryDto query,
        CancellationToken cancellationToken)
        => PendingAsync<AnnualReportDto>(
            "REP-03",
            "年度水电、卫生和门禁汇总口径待审核确认",
            cancellationToken);

    private static string ResolveYearMonth(string? yearMonth)
        => string.IsNullOrWhiteSpace(yearMonth) ? DateTime.Now.ToString("yyyy-MM") : yearMonth;
}
