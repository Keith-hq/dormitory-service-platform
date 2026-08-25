using System.ComponentModel.DataAnnotations;

namespace TemplateDormApi.DTO;

/// <summary>
/// 公告列表项 DTO（对齐 GET /notices 响应，避免直接序列化 EF 双向导航）。
/// </summary>
public class NoticeItemDto
{
    public int NoticeId { get; set; }
    public string? AdminId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTime PublishTime { get; set; }
    public string IsPinned { get; set; } = "否";
    public DateTime? PinTime { get; set; }
}

/// <summary>
/// 公告发布 DTO（对齐契约 POST /notices）
/// </summary>
public class NoticeCreateDto
{
    [Required(ErrorMessage = "公告标题不能为空")]
    [StringLength(100, ErrorMessage = "公告标题最长 100 个字符")]
    public string Title { get; set; } = string.Empty;

    [Required(ErrorMessage = "公告内容不能为空")]
    [StringLength(1000, ErrorMessage = "公告内容最长 1000 个字符")]
    public string Content { get; set; } = string.Empty;

    /// <summary>是否置顶（是 / 否），默认否</summary>
    [RegularExpression("^(是|否)$", ErrorMessage = "置顶标记只能是：是 / 否")]
    public string IsPinned { get; set; } = "否";
}

/// <summary>
/// 公告编辑 DTO
/// </summary>
public class NoticeUpdateDto
{
    [StringLength(100, ErrorMessage = "公告标题最长 100 个字符")]
    public string? Title { get; set; }

    [StringLength(1000, ErrorMessage = "公告内容最长 1000 个字符")]
    public string? Content { get; set; }

    [RegularExpression("^(是|否)$", ErrorMessage = "置顶标记只能是：是 / 否")]
    public string? IsPinned { get; set; }
}
