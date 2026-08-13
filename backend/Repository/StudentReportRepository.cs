using TemplateDormApi.Data;
using TemplateDormApi.DTO;

namespace TemplateDormApi.Repository;

public sealed class StudentReportRepository : FrameworkRepositoryBase
{
    public StudentReportRepository(AppDbContext context) : base(context) { }

    public Task<MonthlyFeeReportDto> GetMonthlyFeeAsync(
        string studentId,
        MonthlyReportQueryDto query,
        CancellationToken cancellationToken)
        => PendingAsync<MonthlyFeeReportDto>(
            "REP-01",
            "普通查询、View 或存储过程的实现方式待审核确认",
            cancellationToken);

    public Task<FacilityUsageReportDto> GetFacilityUsageAsync(
        string studentId,
        MonthlyReportQueryDto query,
        CancellationToken cancellationToken)
        => PendingAsync<FacilityUsageReportDto>(
            "REP-02",
            "设施使用次数统计口径待审核确认",
            cancellationToken);

    public Task<AnnualReportDto> GetAnnualReportAsync(
        string studentId,
        AnnualReportQueryDto query,
        CancellationToken cancellationToken)
        => PendingAsync<AnnualReportDto>(
            "REP-03",
            "年度水电、卫生和门禁汇总口径待审核确认",
            cancellationToken);
}
