namespace TemplateDormApi.Models;

/// <summary>
/// 维修耗材消耗记录实体，对应 D_Repair_Material_Usage。
/// </summary>
public class RepairMaterialUsage
{
    public int UsageId { get; set; }

    public int TicketId { get; set; }

    public int MaterialId { get; set; }

    public int Quantity { get; set; }

    public DateTime UseTime { get; set; }
}
