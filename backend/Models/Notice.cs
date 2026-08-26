using System.ComponentModel.DataAnnotations;

namespace TemplateDormApi.Models;

/// <summary>
/// 公告实体（映射 D_Notice）
/// </summary>
public class Notice
{
    [Key]
    public int NoticeId { get; set; }

    /// <summary>发布人（宿管工号，FK → D_Admin）。历史数据可能为 NULL，声明可空避免读取 ORA-50032</summary>
    public string? AdminId { get; set; }

    /// <summary>公告标题</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>公告内容</summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>发布时间</summary>
    public DateTime PublishTime { get; set; } = DateTime.Now;

    /// <summary>置顶信息（1:1，对应 D_Notice_Display）</summary>
    public NoticeDisplay? Display { get; set; }
}
