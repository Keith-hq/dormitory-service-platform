using TemplateDormApi.DTO;

namespace TemplateDormApi.Services;

/// <summary>
/// 信用分公共业务服务。
/// </summary>
public interface ICreditService
{
    Task<CreditResultDto> DeductAsync(CreditDeductDto dto, CancellationToken cancellationToken);

    Task<CreditStatusDto> GetStatusAsync(string studentId, CancellationToken cancellationToken);

    Task<CreditViewDto> GetViewAsync(
        string studentId,
        int accountId,
        CancellationToken cancellationToken);

    Task<ResetResultDto> ResetMonthlyAsync(int year, int month, CancellationToken cancellationToken);
}
