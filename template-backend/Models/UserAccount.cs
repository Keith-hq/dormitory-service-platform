namespace TemplateDormApi.Models;

/// <summary>
/// 用户账户实体，对应 D_USER_ACCOUNT。
/// </summary>
public class UserAccount
{
    public int AccountId { get; set; }

    public string LoginName { get; set; } = string.Empty;

    public string PasswordHash { get; set; } = string.Empty;

    public string AccountStatus { get; set; } = string.Empty;

    public string? StudentId { get; set; }

    public string? AdminId { get; set; }
}
