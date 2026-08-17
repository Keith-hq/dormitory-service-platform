using TemplateDormApi.DTO;

namespace TemplateDormApi.Services;

/// <summary>
/// 信用分申诉服务（APPEAL-01/02/03）。
/// </summary>
public interface ICreditAppealService
{
    /// <summary>APPEAL-01 提交申诉 — 学生对本人的一条扣分明细申诉</summary>
    Task<CreditAppealDto> SubmitAsync(
        int accountId,
        CreateCreditAppealRequest dto,
        CancellationToken cancellationToken);

    /// <summary>APPEAL-02 查询我的申诉（分页）— 学生本人或宿管</summary>
    Task<PagedResult<CreditAppealDto>> GetMyAsync(
        int accountId,
        string studentId,
        int page,
        int pageSize,
        CancellationToken cancellationToken);

    /// <summary>APPEAL-03 复核申诉（通过恢复信用分 / 驳回）— 楼长/超管</summary>
    Task<CreditAppealDto> ReviewAsync(
        int appealId,
        string reviewerAdminId,
        ReviewCreditAppealRequest dto,
        CancellationToken cancellationToken);
}
