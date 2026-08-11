using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace TemplateDormApi.DTO;

/// <summary>
/// 面向学生端的通知数据。
/// </summary>
public class NotificationItemDto
{
    public int NotificationId { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Content { get; set; } = string.Empty;

    public string NotificationType { get; set; } = string.Empty;

    public DateTime? ReadTime { get; set; }

    public DateTime CreateTime { get; set; }
}

/// <summary>
/// 内部服务投递通知的请求体。
/// </summary>
public class NotificationCreateDto : IValidatableObject
{
    public static readonly IReadOnlySet<string> AllowedNotificationTypes = new HashSet<string>(StringComparer.Ordinal)
    {
        "预约", "报修", "账单", "信用", "访客", "系统"
    };

    [StringLength(20, ErrorMessage = "学生学号不能超过 20 个字符")]
    public string? StudentId { get; set; }

    [StringLength(20, ErrorMessage = "管理员工号不能超过 20 个字符")]
    public string? AdminId { get; set; }

    [Required(ErrorMessage = "通知标题不能为空")]
    [StringLength(100, ErrorMessage = "通知标题不能超过 100 个字符")]
    public string Title { get; set; } = string.Empty;

    [Required(ErrorMessage = "通知内容不能为空")]
    [StringLength(1000, ErrorMessage = "通知内容不能超过 1000 个字符")]
    public string Content { get; set; } = string.Empty;

    [Required(ErrorMessage = "通知类型不能为空")]
    [StringLength(20, ErrorMessage = "通知类型不能超过 20 个字符")]
    [JsonPropertyName("type")]
    public string NotificationType { get; set; } = string.Empty;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        var hasStudentId = !string.IsNullOrWhiteSpace(StudentId);
        var hasAdminId = !string.IsNullOrWhiteSpace(AdminId);

        if (hasStudentId == hasAdminId)
        {
            yield return new ValidationResult(
                "studentId 与 adminId 必须且只能提供一个",
                new[] { nameof(StudentId), nameof(AdminId) });
        }

        if (StudentId is not null && !hasStudentId)
        {
            yield return new ValidationResult("学生学号不能仅包含空白字符", new[] { nameof(StudentId) });
        }

        if (AdminId is not null && !hasAdminId)
        {
            yield return new ValidationResult("管理员工号不能仅包含空白字符", new[] { nameof(AdminId) });
        }

        if (!string.IsNullOrWhiteSpace(NotificationType) &&
            !AllowedNotificationTypes.Contains(NotificationType))
        {
            yield return new ValidationResult(
                "通知类型必须为预约、报修、账单、信用、访客或系统",
                new[] { nameof(NotificationType) });
        }
    }
}

/// <summary>
/// 批量标记已读的请求体。
/// </summary>
public class ReadBatchDto : IValidatableObject
{
    [Required(ErrorMessage = "ids 不能为空")]
    [MinLength(1, ErrorMessage = "ids 至少包含一条通知")]
    [MaxLength(1000, ErrorMessage = "ids 最多包含 1000 条通知")]
    [JsonPropertyName("notificationIds")]
    public List<int>? Ids { get; set; } = new();

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Ids is null)
        {
            yield break;
        }

        if (Ids.Count != Ids.Distinct().Count())
        {
            yield return new ValidationResult("ids 不能包含重复的通知 ID", new[] { nameof(Ids) });
        }

        if (Ids.Any(id => id <= 0))
        {
            yield return new ValidationResult("ids 必须全部为正整数", new[] { nameof(Ids) });
        }
    }
}

/// <summary>
/// 未读通知数量。
/// </summary>
public class UnreadCountDto
{
    public int Count { get; set; }
}
