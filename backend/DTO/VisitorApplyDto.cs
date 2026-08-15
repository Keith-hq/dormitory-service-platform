using System.ComponentModel.DataAnnotations;

namespace TemplateDormApi.DTO;

/// <summary>
/// 申请访客授权请求（STU-32）。
/// 对齐契约并「以 DDL 为准」：契约中的 visitorPhone / startTime 未落入 D_Visitor_Authorization，
/// 故不收；endTime 对应 DDL 的 Expires_Time。
/// </summary>
public class VisitorApplyRequest
{
    [Required(ErrorMessage = "访客姓名不能为空")]
    [StringLength(50, ErrorMessage = "访客姓名最长 50 字符")]
    public string VisitorName { get; set; } = string.Empty;

    [StringLength(200, ErrorMessage = "来访事由最长 200 字符")]
    public string? VisitReason { get; set; }

    /// <summary>授权截止时间（对应 DDL Expires_Time，必填）</summary>
    [Required(ErrorMessage = "授权截止时间不能为空")]
    public DateTime? EndTime { get; set; }
}
