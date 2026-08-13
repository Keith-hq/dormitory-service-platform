namespace TemplateDormApi.Models;

/// <summary>
/// 共享物品借用记录（映射 D_Item_Loan）——退宿三步校验的只读数据源。
/// 写入方为共享物品模块（李昂，难点④），本模块不写此表（跨模块只读，符合单写者红线）。
/// </summary>
public class ItemLoan
{
    public int LoanId { get; set; }

    public int ItemId { get; set; }

    public string StudentId { get; set; } = string.Empty;

    public DateTime BorrowTime { get; set; }

    public DateTime DueTime { get; set; }

    /// <summary>归还时间；NULL = 未归还（退宿校验不通过项）</summary>
    public DateTime? ReturnTime { get; set; }
}
