using System.ComponentModel.DataAnnotations;

namespace TemplateDormApi.Models;

/// <summary>
/// 访客授权实体（映射 D_Visitor_Authorization，主键由序列+触发器生成）
/// </summary>
public class VisitorAuthorization
{
    [Key]
    public int AuthorizationId { get; set; }

    /// <summary>申请学生（FK → D_Student）</summary>
    public string StudentId { get; set; } = string.Empty;

    /// <summary>所属房间（FK → D_Room）</summary>
    public int RoomId { get; set; }

    /// <summary>访客姓名</summary>
    public string VisitorName { get; set; } = string.Empty;

    /// <summary>来访事由</summary>
    public string? VisitReason { get; set; }

    /// <summary>授权凭证（二维码 token，唯一）</summary>
    public string AuthorizationToken { get; set; } = string.Empty;

    /// <summary>过期时间</summary>
    public DateTime ExpiresTime { get; set; }

    /// <summary>状态（有效 / 已过期 / 已撤销）</summary>
    public string Status { get; set; } = "有效";

    /// <summary>创建时间</summary>
    public DateTime CreateTime { get; set; }
}
