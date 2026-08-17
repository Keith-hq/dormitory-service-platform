using System.ComponentModel.DataAnnotations;

namespace TemplateDormApi.DTO;

/// <summary>APPEAL-01 提交申诉 — POST /credit-appeals</summary>
public class CreateCreditAppealRequest
{
    [Range(1, int.MaxValue, ErrorMessage = "扣分明细不能为空")]
    public int CreditRecordId { get; set; }

    [Required(ErrorMessage = "申诉原因不能为空")]
    [MaxLength(200, ErrorMessage = "申诉原因不能超过 200 个字符")]
    public string Reason { get; set; } = string.Empty;
}

/// <summary>APPEAL-03 复核申诉 — PUT /credit-appeals/{appealId}/review</summary>
public class ReviewCreditAppealRequest
{
    /// <summary>通过 / 驳回（契约 enum）</summary>
    [Required(ErrorMessage = "复核结论不能为空")]
    public string Result { get; set; } = string.Empty;

    /// <summary>处理说明（可特别注明）</summary>
    [MaxLength(200, ErrorMessage = "处理说明不能超过 200 个字符")]
    public string? Note { get; set; }
}

/// <summary>申诉记录响应项</summary>
public class CreditAppealDto
{
    public int AppealId { get; set; }
    public int CreditRecordId { get; set; }
    public string StudentId { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? ResultDesc { get; set; }
    public string? ReviewedBy { get; set; }
    public DateTime? ReviewTime { get; set; }
    public DateTime CreateTime { get; set; }
    /// <summary>被申诉扣分的明细（供展示）</summary>
    public int? ScoreChange { get; set; }
    public string? CreditReason { get; set; }
}
