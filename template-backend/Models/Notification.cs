namespace TemplateDormApi.Models;

/// <summary>
/// 通知实体，对应 D_NOTIFICATION。
/// </summary>
public class Notification
{
    public int NotificationId { get; set; }

    public int RecipientAccountId { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Content { get; set; } = string.Empty;

    public string NotificationType { get; set; } = string.Empty;

    /// <summary>NULL 表示未读。</summary>
    public DateTime? ReadTime { get; set; }

    /// <summary>由数据库默认值 SYSDATE 生成。</summary>
    public DateTime CreateTime { get; set; }
}
