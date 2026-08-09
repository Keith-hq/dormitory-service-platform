namespace TemplateDormApi.Models;

/// <summary>
/// 信用分流水实体，对应 D_CREDIT_LOG。
/// </summary>
public class CreditLog
{
    public int LogId { get; set; }

    public string StudentId { get; set; } = string.Empty;

    public int ScoreChange { get; set; }

    public string Reason { get; set; } = string.Empty;

    public string EventKey { get; set; } = string.Empty;

    public DateTime CreateTime { get; set; }
}
