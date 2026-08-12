namespace TemplateDormApi.Models;

/// <summary>
/// 维修日志实体，对应 D_Repair_Log。
/// UK_D_REPAIR_LOG_TICKET：一工单仅一条完工日志。
/// </summary>
public class RepairLog
{
    public int LogId { get; set; }

    public int TicketId { get; set; }

    public string AdminId { get; set; } = string.Empty;

    public string ProcessDesc { get; set; } = string.Empty;

    /// <summary>维修结果（对齐契约 result 字段）</summary>
    public string? RepairResult { get; set; }

    public DateTime ResolveTime { get; set; }
}
