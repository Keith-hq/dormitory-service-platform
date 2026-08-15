namespace TemplateDormApi.Models;

/// <summary>
/// 共享物品实体，对应 D_Shared_Item。
/// 主数据维护=刘鸿铭，库存扣减=李昂（难点④ SP_Borrow_Item / SP_Return_Item）。
/// </summary>
public class SharedItem
{
    public int ItemId { get; set; }

    public string ItemName { get; set; } = string.Empty;

    public int BuildingId { get; set; }

    public int TotalQty { get; set; }

    public int AvailableQty { get; set; }

    public string Status { get; set; } = "正常";
}
