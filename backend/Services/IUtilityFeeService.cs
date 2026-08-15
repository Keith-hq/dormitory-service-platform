using TemplateDormApi.DTO;

namespace TemplateDormApi.Services;

/// <summary>
/// 水电账单服务接口（DORM-19~24 账单生命周期 + DORM-25 供电状态查询）。
/// 发布/分摊前的账单可改，已发布账单受条件 UPDATE 保护（防发布竞态）。
/// </summary>
public interface IUtilityFeeService
{
    /// <summary>DORM-19 录账单（返回新账单 Fee_ID，由序列触发器回填）</summary>
    Task<long> CreateBill(CreateUtilityFeeRequest request);

    /// <summary>DORM-20 修改账单（仅未发布可改）</summary>
    Task UpdateBill(long feeId, UpdateUtilityFeeRequest request);

    /// <summary>DORM-21 发布账单（幂等：已发布返回 400，条件 UPDATE 防竞态）</summary>
    Task PublishBill(long feeId);

    /// <summary>
    /// DORM-22 账单分摊。调用月度分摊 SP（自 COMMIT 幂等，整月维度——
    /// 该月全部已发布账单一起分摊，返回值为本账单生成/已有的明细条数）。
    /// </summary>
    Task<AllocateResultDto> AllocateBill(long feeId);

    /// <summary>DORM-23 查询单笔账单的分摊明细</summary>
    Task<UtilityFeeDetailsDto> GetBillDetails(long feeId);

    /// <summary>DORM-24 账单列表（按账期/发布状态过滤）</summary>
    Task<List<UtilityFeeListItemDto>> GetBills(string? yearMonth, string? publishStatus);
}
