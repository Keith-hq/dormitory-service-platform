using System.ComponentModel.DataAnnotations;

namespace TemplateDormApi.DTO;

/// <summary>STU-15 提交离校报备 — 契约 POST /leave-applications body</summary>
public class LeaveSubmitDto
{
    [Required(ErrorMessage = "学号不能为空")]
    public string StudentId { get; set; } = string.Empty;

    [Required(ErrorMessage = "离校日期不能为空")]
    public DateTime LeaveDate { get; set; }

    [Required(ErrorMessage = "返校日期不能为空")]
    public DateTime ReturnDate { get; set; }

    [Required(ErrorMessage = "目的地不能为空")]
    [MaxLength(200)]
    public string Destination { get; set; } = string.Empty;
}

/// <summary>STU-17 修改报备 — 契约 PUT /leave-applications/{applyId} body（仅待批可用）</summary>
public class LeaveUpdateDto
{
    public DateTime? LeaveDate { get; set; }

    public DateTime? ReturnDate { get; set; }

    [MaxLength(200)]
    public string? Destination { get; set; }
}

/// <summary>COUN-03 驳回报备 — 契约 PUT /leave-applications/{applyId}/reject body（驳回必填原因）</summary>
public class LeaveRejectDto
{
    [Required(ErrorMessage = "驳回原因不能为空")]
    [MaxLength(200)]
    public string Reason { get; set; } = string.Empty;
}
