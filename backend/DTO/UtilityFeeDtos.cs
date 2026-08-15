using System.ComponentModel.DataAnnotations;

namespace TemplateDormApi.DTO;

/// <summary>
/// DORM-19 录账单请求体。
/// 契约字段名 elecFee 对应表列 D_Utility_Fee.Power_Fee（前端契约用 elecFee 命名）。
/// </summary>
public class CreateUtilityFeeRequest
{
    [Range(1, long.MaxValue, ErrorMessage = "房间ID必须大于0")]
    public long RoomId { get; set; }

    /// <summary>账期 yyyy-MM（控制器校验格式与范围）</summary>
    public string YearMonth { get; set; } = string.Empty;

    /// <summary>
    /// 水费。nullable + [Required]：区分"漏传"与"合法 0 元"（PR #58 P2 整改）；
    /// 缺失时模型绑定 400，服务层再兜底一次（绕过绑定的直接调用）。
    /// </summary>
    [Required(ErrorMessage = "水费为必填字段")]
    [Range(typeof(decimal), "0", "999999.99", ErrorMessage = "水费必须在 0~999999.99 之间")]
    public decimal? WaterFee { get; set; }

    /// <summary>电费（契约字段 elecFee ↔ 表列 Power_Fee，必填同水费）</summary>
    [Required(ErrorMessage = "电费为必填字段")]
    [Range(typeof(decimal), "0", "999999.99", ErrorMessage = "电费必须在 0~999999.99 之间")]
    public decimal? ElecFee { get; set; }
}

/// <summary>DORM-20 修改账单请求体（未分摊未缴可改——已发布未分摊也允许，见服务注释）</summary>
public class UpdateUtilityFeeRequest
{
    [Range(typeof(decimal), "0", "999999.99", ErrorMessage = "水费必须在 0~999999.99 之间")]
    public decimal WaterFee { get; set; }

    /// <summary>电费（契约字段 elecFee ↔ 表列 Power_Fee）</summary>
    [Range(typeof(decimal), "0", "999999.99", ErrorMessage = "电费必须在 0~999999.99 之间")]
    public decimal ElecFee { get; set; }
}

/// <summary>DORM-24 账单列表条目</summary>
public class UtilityFeeListItemDto
{
    public long FeeId { get; set; }
    public long RoomId { get; set; }
    public string YearMonth { get; set; } = string.Empty;
    public decimal? WaterFee { get; set; }
    public decimal? PowerFee { get; set; }
    public string? IsPaid { get; set; }
    public string PublishStatus { get; set; } = string.Empty;
}

/// <summary>
/// DORM-23 分摊明细条目。字段对齐 IT-C3-002 通过判定①：
/// stayDays / totalDays / waterShare / powerShare / total / billType。
/// </summary>
public class UtilityFeeDetailItemDto
{
    public int DetailId { get; set; }
    public string StudentId { get; set; } = string.Empty;
    public int RoomId { get; set; }
    public int StayDays { get; set; }
    public int TotalDays { get; set; }
    public decimal WaterShare { get; set; }
    public decimal PowerShare { get; set; }
    /// <summary>个人应缴合计 = WaterShare + PowerShare</summary>
    public decimal Total { get; set; }
    public string BillType { get; set; } = string.Empty;
    public string IsPaid { get; set; } = string.Empty;
}

/// <summary>DORM-23 单笔账单的分摊明细视图</summary>
public class UtilityFeeDetailsDto
{
    public long FeeId { get; set; }
    public long RoomId { get; set; }
    public string YearMonth { get; set; } = string.Empty;
    public List<UtilityFeeDetailItemDto> Items { get; set; } = new();
}

/// <summary>DORM-22 分摊结果</summary>
public class AllocateResultDto
{
    public long FeeId { get; set; }
    public string YearMonth { get; set; } = string.Empty;
    /// <summary>本账单生成的分摊明细条数</summary>
    public int DetailCount { get; set; }
}
