using System.ComponentModel.DataAnnotations;

namespace TemplateDormApi.Models;

/// <summary>
/// 资产实体（映射 D_Asset：固定资产台账，只做盘点）
/// </summary>
public class Asset
{
    [Key]
    public int AssetId { get; set; }

    /// <summary>所属房间ID（DDL 可空）</summary>
    public int? RoomId { get; set; }

    /// <summary>资产名称（如：空调、床铺）</summary>
    public string AssetName { get; set; } = string.Empty;

    /// <summary>数量（DDL 列 Quantity，可空，默认 1 由 DB 兜底）</summary>
    public int? Quantity { get; set; }

    /// <summary>状态（正常 / 损坏 / 缺失）</summary>
    public string Status { get; set; } = "正常";
}
