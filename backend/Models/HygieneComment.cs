namespace TemplateDormApi.Models;

/// <summary>
/// 卫生评分评语，对应 D_HYGIENE_COMMENT，与卫生评分共享主键。
/// </summary>
public sealed class HygieneComment
{
    public long RecordId { get; set; }
    public string? CommentText { get; set; }
    public HygieneRecord Record { get; set; } = null!;
}
