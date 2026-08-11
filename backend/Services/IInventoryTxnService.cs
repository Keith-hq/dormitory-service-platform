using TemplateDormApi.Models;

namespace TemplateDormApi.Services;

/// <summary>
/// 共享物品借还与耗材出库服务接口（难点④）。
/// 库存扣减全部走存储过程的乐观锁（WHERE Available_Qty > 0 / Stock_Qty >= n + SQL%ROWCOUNT）。
/// </summary>
public interface IInventoryTxnService
{
    /// <summary>借用共享物品，返回 (resultCode, loanId)</summary>
    Task<(int resultCode, int loanId)> BorrowItem(int itemId, string studentId);

    /// <summary>归还共享物品，返回 resultCode</summary>
    Task<int> ReturnItem(int loanId);

    /// <summary>维修耗材出库，返回 resultCode</summary>
    Task<int> ConsumeMaterial(int materialId, int ticketId, int quantity);

    /// <summary>逾期巡检（定时任务调用）</summary>
    Task CheckOverdue();

    /// <summary>查询可借共享物品列表</summary>
    Task<List<SharedItem>> GetSharedItems(int? buildingId = null);

    /// <summary>查询学生借还记录</summary>
    Task<List<ItemLoan>> GetItemLoans(string studentId);

    /// <summary>查询耗材库存</summary>
    Task<List<RepairMaterial>> GetRepairMaterials();
}
