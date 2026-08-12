using TemplateDormApi.DTO;
using TemplateDormApi.Repository;

namespace TemplateDormApi.Services;

public interface IStudentReportService
{
    Task<MonthlyFeeReportDto> GetMonthlyFeeAsync(string studentId, int accountId, MonthlyReportQueryDto query, CancellationToken cancellationToken);
    Task<FacilityUsageReportDto> GetFacilityUsageAsync(string studentId, int accountId, MonthlyReportQueryDto query, CancellationToken cancellationToken);
    Task<AnnualReportDto> GetAnnualReportAsync(string studentId, int accountId, AnnualReportQueryDto query, CancellationToken cancellationToken);
}

public sealed class StudentReportService : IStudentReportService
{
    private readonly StudentReportRepository _repository;
    private readonly IStudentIdentityService _identityService;

    public StudentReportService(
        StudentReportRepository repository,
        IStudentIdentityService identityService)
    {
        _repository = repository;
        _identityService = identityService;
    }

    public async Task<MonthlyFeeReportDto> GetMonthlyFeeAsync(
        string studentId,
        int accountId,
        MonthlyReportQueryDto query,
        CancellationToken cancellationToken)
    {
        await _identityService.EnsureOwnStudentIdAsync(accountId, studentId, cancellationToken);
        return await _repository.GetMonthlyFeeAsync(studentId, query, cancellationToken);
    }

    public async Task<FacilityUsageReportDto> GetFacilityUsageAsync(
        string studentId,
        int accountId,
        MonthlyReportQueryDto query,
        CancellationToken cancellationToken)
    {
        await _identityService.EnsureOwnStudentIdAsync(accountId, studentId, cancellationToken);
        return await _repository.GetFacilityUsageAsync(studentId, query, cancellationToken);
    }

    public async Task<AnnualReportDto> GetAnnualReportAsync(
        string studentId,
        int accountId,
        AnnualReportQueryDto query,
        CancellationToken cancellationToken)
    {
        await _identityService.EnsureOwnStudentIdAsync(accountId, studentId, cancellationToken);
        return await _repository.GetAnnualReportAsync(studentId, query, cancellationToken);
    }
}
