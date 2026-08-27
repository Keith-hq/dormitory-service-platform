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

    /// <summary>
    /// 流水时间：默认取本机本地时间（北京时间），避免依赖数据库 SYSDATE 的 UTC 会话时区
    /// 导致记录比实际晚 8 小时（凌晨创建的记录会显示成前一天）。
    /// </summary>
    public DateTime CreateTime { get; set; } = DateTime.Now;
}
