namespace TemplateDormApi.Models;

/// <summary>
/// 信用分账户实体，对应 D_CREDIT_ACCOUNT。
/// </summary>
public class CreditAccount
{
    public string StudentId { get; set; } = string.Empty;

    public int CurrentScore { get; set; } = 100;

    public DateTime UpdatedTime { get; set; }
}
