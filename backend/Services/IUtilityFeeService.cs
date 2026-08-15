using TemplateDormApi.DTO;

namespace TemplateDormApi.Services;

/// <summary>
/// 水电账单服务接口（DORM-19~24 账单生命周期 + DORM-25 供电状态查询）。
/// 修改：无分摊明细可改（已发布未分摊也允许，DORM-20 契约口径），
/// 条件 UPDATE（NOT EXISTS 明细）防分摊竞态；发布：条件 UPDATE 防发布竞态。
/// </summary>
public interface IUtilityFeeService
{
    /// <summary>DORM-19 录账单（返回新账单 Fee_ID，由序列触发器回填）</summary>
    Task<long> CreateBill(CreateUtilityFeeRequest request);

    /// <summary>DORM-20 修改账单（存在分摊明细即禁止——已分摊或已缴不可改）</summary>
    Task UpdateBill(long feeId, UpdateUtilityFeeRequest request);

    /// <summary>DORM-21 发布账单（幂等：已发布返回 400；发布成功自动触发该月分摊）</summary>
    Task PublishBill(long feeId);

    /// <summary>
    /// DORM-22 账单分摊。调用月度分摊 SP（自 COMMIT 幂等，整月维度——
    /// 该月全部已发布账单一起分摊，返回值为本账单生成/已有的明细条数）。
    /// </summary>
    Task<AllocateResultDto> AllocateBill(long feeId);

    /// <summary>DORM-23 查询单笔账单的分摊明细</summary>
    Task<UtilityFeeDetailsDto> GetBillDetails(long feeId);

    /// <summary>
    /// DORM-24 账单列表（按楼栋/账期/缴费状态/发布状态过滤，分页返回）。
    /// isPaid=true 表示账单无未缴明细（全部缴清；无明细视为已缴清），
    /// isPaid=false 表示账单存在未缴明细（以 D_Fee_Detail 为权威，非账单行 Is_Paid 列）。
    /// </summary>
    Task<PagedResult<UtilityFeeListItemDto>> GetBills(
        long? buildingId, string? yearMonth, bool? isPaid, string? publishStatus, int page, int pageSize);
}
