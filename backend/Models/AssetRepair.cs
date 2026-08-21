namespace TemplateDormApi.Models;

/// <summary>
/// 资产↔报修工单关联（扩展表 D_Asset_Repair）。
/// D_Repair_Ticket 属 foundation 冻结表、无 Asset_ID 列，故用本表记录关联；
/// 应用层据此实现 DORM-16「防重复工单」与 DORM-14-delete「已关联报修的资产禁止删除」。
/// </summary>
public class AssetRepair
{
    public int LinkId { get; set; }

    public int AssetId { get; set; }

    public long TicketId { get; set; }

    public DateTime CreateTime { get; set; }
}
