namespace TemplateDormApi.Models;

/// <summary>
/// 共享物品借还记录实体，对应 D_Item_Loan。
/// 写入必须走存储过程（SP_Borrow_Item / SP_Return_Item）：Loan_ID 由序列生成、
/// Idempotency_Key 由 SP 落库并受唯一索引 UK_D_ITEM_LOAN_IDEM 兜底（迁移 019）。
/// 退宿两步校验（DORM-36）仅只读查询本表（Return_Time IS NULL = 未归还，校验不通过项）。
/// </summary>
public class ItemLoan
{
    public int LoanId { get; set; }

    public int ItemId { get; set; }

    public string StudentId { get; set; } = string.Empty;

    public DateTime BorrowTime { get; set; }

    public DateTime DueTime { get; set; }

    public DateTime? ReturnTime { get; set; }

    /// <summary>幂等键（迁移 019，VARCHAR2(100 CHAR)）；SP 层以"键非空"为前提做幂等去重。</summary>
    public string? IdempotencyKey { get; set; }
}
