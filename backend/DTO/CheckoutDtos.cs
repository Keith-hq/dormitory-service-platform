using System.ComponentModel.DataAnnotations;

namespace TemplateDormApi.DTO;

/// <summary>
/// DORM-11 退宿登记 — 契约 POST /allocations/{allocationId}/checkout-register body。
/// 说明：D_Checkout_Log 无退宿原因/计划退宿日期列（DDL 冻结不改），
/// 这两个字段仅作契约兼容接收，不落库（挂账：若需持久化需新增列，待裁决）。
/// </summary>
public class CheckoutRegisterDto
{
    [MaxLength(200)]
    public string? Reason { get; set; }

    public DateTime? CheckoutDate { get; set; }
}

/// <summary>DORM-37 确认退宿 — 契约 POST /checkouts/{checkoutId}/confirm body（幂等）</summary>
public class CheckoutConfirmDto
{
    /// <summary>退宿日期；为空时取当前时间。settle 已写入则保持已写值（幂等语义）。</summary>
    public DateTime? CheckoutDate { get; set; }
}
