using System.ComponentModel.DataAnnotations;

namespace TemplateDormApi.DTO;

/// <summary>
/// 内部服务加减信用分请求。
/// </summary>
public class CreditDeductDto : IValidatableObject
{
    [Required(ErrorMessage = "学生学号不能为空")]
    [StringLength(20, ErrorMessage = "学生学号不能超过 20 个字符")]
    public string StudentId { get; set; } = string.Empty;

    [Range(-100, 100, ErrorMessage = "scoreChange 必须在 -100 到 100 之间")]
    public int ScoreChange { get; set; }

    [Required(ErrorMessage = "变更原因不能为空")]
    [StringLength(200, ErrorMessage = "变更原因不能超过 200 个字符")]
    public string Reason { get; set; } = string.Empty;

    [Required(ErrorMessage = "Event_Key 不能为空")]
    [StringLength(100, ErrorMessage = "Event_Key 不能超过 100 个字符")]
    public string EventKey { get; set; } = string.Empty;

    /// <summary>
    /// 罚分封底：为 true 时，扣分在信用分服务锁内按当前分数封底到 0，
    /// 不再因"结果低于 0"被拒绝。供按次罚分（如共享物品超期归还扣 2 分）使用，
    /// 避免低分学生（1 分）扣分失败导致"已归还但扣分永久不一致"。
    /// 其他扣款场景（如账单扣款）保持 false，扣款不足仍应拒绝。
    /// </summary>
    public bool FloorAtZero { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (ScoreChange == 0)
        {
            yield return new ValidationResult("scoreChange 不能为 0", new[] { nameof(ScoreChange) });
        }

        if (string.IsNullOrWhiteSpace(StudentId))
        {
            yield return new ValidationResult("学生学号不能仅包含空白字符", new[] { nameof(StudentId) });
        }

        if (string.IsNullOrWhiteSpace(Reason))
        {
            yield return new ValidationResult("变更原因不能仅包含空白字符", new[] { nameof(Reason) });
        }

        if (string.IsNullOrWhiteSpace(EventKey))
        {
            yield return new ValidationResult("Event_Key 不能仅包含空白字符", new[] { nameof(EventKey) });
        }
    }
}

public class CreditStatusDto
{
    public string StudentId { get; set; } = string.Empty;
    public int CurrentScore { get; set; }
    public bool IsFrozen { get; set; }
}

public class CreditResultDto : CreditStatusDto
{
}

public class CreditLogItemDto
{
    public string Reason { get; set; } = string.Empty;
    public int ScoreChange { get; set; }
    public DateTime CreateTime { get; set; }
}

public class CreditViewDto : CreditStatusDto
{
    public List<CreditLogItemDto> Items { get; set; } = new();
}

public class ResetResultDto
{
    public int Processed { get; set; }
    public int Skipped { get; set; }
    public int Failed { get; set; }
}
