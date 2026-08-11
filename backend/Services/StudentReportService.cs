using TemplateDormApi.DTO;
using TemplateDormApi.Repository;

namespace TemplateDormApi.Services;

public interface IStudentReportService
{
    Task<MonthlyFeeReportDto> GetMonthlyFeeAsync(string studentId, MonthlyReportQueryDto query, CancellationToken cancellationToken);
    Task<FacilityUsageReportDto> GetFacilityUsageAsync(string studentId, MonthlyReportQueryDto query, CancellationToken cancellationToken);
    Task<AnnualReportDto> GetAnnualReportAsync(string studentId, AnnualReportQueryDto query, CancellationToken cancellationToken);
}

public sealed class StudentReportService : IStudentReportService
{
    private readonly StudentReportRepository _repository;

    public StudentReportService(StudentReportRepository repository)
    {
        _repository = repository;
    }

    public Task<MonthlyFeeReportDto> GetMonthlyFeeAsync(
        string studentId,
        MonthlyReportQueryDto query,
        CancellationToken cancellationToken)
        => _repository.GetMonthlyFeeAsync(studentId, query, cancellationToken);

    public Task<FacilityUsageReportDto> GetFacilityUsageAsync(
        string studentId,
        MonthlyReportQueryDto query,
        CancellationToken cancellationToken)
        => _repository.GetFacilityUsageAsync(studentId, query, cancellationToken);

    public Task<AnnualReportDto> GetAnnualReportAsync(
        string studentId,
        AnnualReportQueryDto query,
        CancellationToken cancellationToken)
        => _repository.GetAnnualReportAsync(studentId, query, cancellationToken);
}
