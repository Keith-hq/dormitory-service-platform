using System.ComponentModel.DataAnnotations;

namespace TemplateDormApi.DTO;

/// <summary>登记资产请求（DORM-13）</summary>
public sealed class CreateAssetRequest
{
    [Range(1, int.MaxValue, ErrorMessage = "所属房间不能为空")]
    public int RoomId { get; set; }

    [Required(ErrorMessage = "资产名称不能为空")]
    [StringLength(50, ErrorMessage = "资产名称最长 50 个字符")]
    public string AssetName { get; set; } = string.Empty;

    [Range(0, int.MaxValue, ErrorMessage = "数量必须大于等于 0")]
    public int Quantity { get; set; } = 1;

    [RegularExpression("^(正常|损坏|缺失)$", ErrorMessage = "状态只能是：正常 / 损坏 / 缺失")]
    public string Status { get; set; } = "正常";
}

/// <summary>
/// 修改资产请求（DORM-14-update）。
/// remark 为契约可选字段，D_ASSET（foundation 冻结）无对应列，不落库。
/// </summary>
public sealed class UpdateAssetRequest
{
    [StringLength(50, ErrorMessage = "资产名称最长 50 个字符")]
    public string? AssetName { get; set; }

    [RegularExpression("^(正常|损坏|缺失)$", ErrorMessage = "状态只能是：正常 / 损坏 / 缺失")]
    public string? Status { get; set; }

    [StringLength(200, ErrorMessage = "备注最长 200 个字符")]
    public string? Remark { get; set; }
}

/// <summary>
/// 资产盘点请求（DORM-15）。
/// note 为契约可选字段，D_ASSET（foundation 冻结）无对应列，不落库。
/// </summary>
public sealed class StocktakeAssetRequest
{
    [Range(0, int.MaxValue, ErrorMessage = "数量必须大于等于 0")]
    public int Quantity { get; set; }

    [StringLength(500, ErrorMessage = "盘点说明最长 500 个字符")]
    public string? Note { get; set; }
}

/// <summary>损坏资产转报修请求（DORM-16）</summary>
public sealed class ToRepairRequest
{
    [Required(ErrorMessage = "报修描述不能为空")]
    [StringLength(500, ErrorMessage = "报修描述最长 500 个字符")]
    public string Description { get; set; } = string.Empty;
}

/// <summary>处理损耗预警请求（DORM-18）</summary>
public sealed class HandleWarningRequest
{
    [Required(ErrorMessage = "处理动作不能为空")]
    [RegularExpression("^(处理|标记重点)$", ErrorMessage = "处理动作只能是：处理 / 标记重点")]
    public string Action { get; set; } = string.Empty;

    [StringLength(500, ErrorMessage = "说明最长 500 个字符")]
    public string? Note { get; set; }
}

/// <summary>资产响应（DORM-12/13/14）</summary>
public sealed class AssetDto
{
    public int AssetId { get; set; }
    public int? RoomId { get; set; }
    public string AssetName { get; set; } = string.Empty;
    public int? Quantity { get; set; }
    public string Status { get; set; } = "正常";
}

/// <summary>损耗预警响应（DORM-17）</summary>
public sealed class AssetWarningDto
{
    public int WarningId { get; set; }
    public int AssetId { get; set; }
    public string AssetName { get; set; } = string.Empty;
    public int? RoomId { get; set; }
    public DateTime CreateTime { get; set; }
    public string? Note { get; set; }
    public string? HandleAction { get; set; }
    public DateTime? HandleTime { get; set; }
    public string Handled { get; set; } = "否";
}
