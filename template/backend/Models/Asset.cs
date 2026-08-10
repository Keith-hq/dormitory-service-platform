using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TemplateDormApi.Models;

/// <summary>
/// 资产实体
/// </summary>
public class Asset
{
    [Key]
    public int AssetId { get; set; }

    /// <summary>所属房间ID</summary>
    public int RoomId { get; set; }

    /// <summary>资产名称（如：空调、床铺）</summary>
    public string AssetName { get; set; } = string.Empty;

    /// <summary>资产类型（家具 / 电器 / 卫浴）</summary>
    public string AssetType { get; set; } = string.Empty;

    /// <summary>是否损坏</summary>
    public bool IsDamaged { get; set; } = false;

    /// <summary>采购日期</summary>
    public DateTime? PurchaseDate { get; set; }
}
