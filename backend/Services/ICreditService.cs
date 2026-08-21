using TemplateDormApi.DTO;

namespace TemplateDormApi.Services;

/// <summary>
/// 信用分公共业务服务。
/// </summary>
public interface ICreditService
{
    Task<CreditResultDto> DeductAsync(CreditDeductDto dto, CancellationToken cancellationToken);

    /// <summary>申诉通过后恢复信用分：追加正向信用流水并更新账户（EventKey 幂等）。</summary>
    Task<CreditResultDto> RestoreAsync(
        string studentId,
        int restoreScore,
        string eventKey,
        string reason,
        CancellationToken cancellationToken);

    Task<CreditStatusDto> GetStatusAsync(string studentId, CancellationToken cancellationToken);

    Task<CreditViewDto> GetViewAsync(
        string studentId,
        int accountId,
        CancellationToken cancellationToken);

    Task<ResetResultDto> ResetMonthlyAsync(int year, int month, CancellationToken cancellationToken);
}
