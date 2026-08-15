namespace TemplateDormApi.Models;

/// <summary>
/// 维修耗材实体，对应 D_Repair_Material。
/// </summary>
public class RepairMaterial
{
    public int MaterialId { get; set; }

    public string MaterialName { get; set; } = string.Empty;

    public string Unit { get; set; } = string.Empty;

    public int StockQty { get; set; }
}
