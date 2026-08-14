using TemplateDormApi.DTO;
using TemplateDormApi.Models;

namespace TemplateDormApi.Services;

/// <summary>
/// 共享物品借还与耗材出库服务接口（难点④）。
/// 库存扣减全部走存储过程的乐观锁（WHERE Available_Qty > 0 / Stock_Qty >= n + SQL%ROWCOUNT）；
/// 信用分扣分统一走信用分公共服务（Event_Key 幂等 + 串行化 + 冻结通知）。
/// </summary>
public interface IInventoryTxnService
{
    /// <summary>借用共享物品，返回 (resultCode, loanId)。idempotencyKey 用于幂等防重。</summary>
    Task<(int resultCode, int loanId)> BorrowItem(int itemId, string studentId, string? idempotencyKey);

    /// <summary>
    /// 归还共享物品，校验 studentId 归属；超期归还经信用分统一入口按次扣 2 分。
    /// 返回 (ResultCode, CreditPending)：CreditPending=true 表示归还已生效但
    /// 超期扣分未完成（信用服务临时失败），由巡检自愈补扣。
    /// </summary>
    Task<(int ResultCode, bool CreditPending)> ReturnItem(int loanId, string studentId);

    /// <summary>维修耗材出库，idempotencyKey 用于幂等防重，返回 resultCode</summary>
    Task<int> ConsumeMaterial(int materialId, int ticketId, int quantity, string? idempotencyKey);

    /// <summary>逾期巡检（定时任务调用）：发逾期提醒（通知公共服务）+ 自愈补扣待补偿扣分</summary>
    Task CheckOverdue();

    /// <summary>查询可借共享物品列表</summary>
    Task<List<SharedItem>> GetSharedItems(int? buildingId = null);

    /// <summary>分页查询学生借还记录</summary>
    Task<PagedResult<ItemLoan>> GetItemLoans(string studentId, int page, int pageSize);

    /// <summary>查询耗材库存</summary>
    Task<List<RepairMaterial>> GetRepairMaterials();
}
