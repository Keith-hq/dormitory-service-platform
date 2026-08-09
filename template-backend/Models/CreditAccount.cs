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

/// <summary>
/// 仅用于锁定 D_STUDENT 父行，串行化信用账户的懒创建。
/// </summary>
internal class CreditStudentLock
{
    public string StudentId { get; set; } = string.Empty;
}
