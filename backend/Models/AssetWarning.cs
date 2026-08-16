namespace TemplateDormApi.Models;

/// <summary>
/// 损耗预警记录（扩展表 D_Asset_Warning）。
/// D_Asset 属 foundation 冻结表、无预警列，故用本表记录预警的产生（DORM-16
/// 损坏转报修时落一条 Handled='否'）与处理痕迹（DORM-18）：处理后 Handled
/// 置为 '是' 退出预警列表；再次损坏产生新预警行重新进入。
/// </summary>
public class AssetWarning
{
    public int WarningId { get; set; }

    public int AssetId { get; set; }

    public DateTime CreateTime { get; set; }

    public string? Note { get; set; }

    /// <summary>处理动作（处理 / 标记重点），未处理时为空</summary>
    public string? HandleAction { get; set; }

    public DateTime? HandleTime { get; set; }

    /// <summary>是否已处理（是 / 否）</summary>
    public string Handled { get; set; } = "否";
}
