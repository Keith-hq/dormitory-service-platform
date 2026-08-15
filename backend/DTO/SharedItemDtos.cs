using System.ComponentModel.DataAnnotations;

namespace TemplateDormApi.DTO;

/// <summary>发布共享物品请求（DORM-48）</summary>
public sealed class CreateSharedItemRequest
{
    [Required(ErrorMessage = "物品名称不能为空")]
    [StringLength(50, ErrorMessage = "物品名称最长 50 个字符")]
    public string Name { get; set; } = string.Empty;

    [Range(0, int.MaxValue, ErrorMessage = "数量必须大于等于 0")]
    public int Quantity { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "所属楼栋不能为空")]
    public int BuildingId { get; set; }

    [StringLength(200, ErrorMessage = "描述最长 200 个字符")]
    public string? Description { get; set; }
}

/// <summary>
/// 维护共享物品请求（DORM-49-update）。
/// status（正常/停用）为契约参数缺口补入字段——契约未提供停用入口，但
/// IT-C5-006 要求"停用后不可借"，经 DORM-49 承载。
/// </summary>
public sealed class UpdateSharedItemRequest
{
    [Range(0, int.MaxValue, ErrorMessage = "数量必须大于等于 0")]
    public int? Quantity { get; set; }

    [StringLength(200, ErrorMessage = "描述最长 200 个字符")]
    public string? Description { get; set; }

    [RegularExpression("^(正常|停用)$", ErrorMessage = "状态只能是：正常 / 停用")]
    public string? Status { get; set; }
}

/// <summary>共享物品主数据响应（DORM-48/49）</summary>
public sealed class SharedItemDto
{
    public int ItemId { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public int BuildingId { get; set; }
    public int TotalQty { get; set; }
    public int AvailableQty { get; set; }
    public string Status { get; set; } = "正常";
    public string? Description { get; set; }
}
