namespace TemplateDormApi.Models;

/// <summary>
/// 共享物品借还记录实体，对应 D_Item_Loan。
/// </summary>
public class ItemLoan
{
    public int LoanId { get; set; }

    public int ItemId { get; set; }

    public string StudentId { get; set; } = string.Empty;

    public DateTime BorrowTime { get; set; }

    public DateTime DueTime { get; set; }

    public DateTime? ReturnTime { get; set; }
}
