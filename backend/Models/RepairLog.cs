namespace TemplateDormApi.Models;

/// <summary>
/// 维修日志，对应 D_REPAIR_LOG。
/// UK_D_REPAIR_LOG_TICKET：一工单仅一条完工日志。
/// RepairResult 为难点⑤ 迁移 021 新增列（完工结果，对齐契约 result 字段）。
/// </summary>
public sealed class RepairLog
{
    public long LogId { get; set; }
    public long? TicketId { get; set; }
    public string? AdminId { get; set; }
    public string? ProcessDescription { get; set; }

    /// <summary>维修结果（对齐契约 result 字段，难点⑤）</summary>
    public string? RepairResult { get; set; }

    public DateTime ResolveTime { get; set; }
    public RepairTicket? Ticket { get; set; }
}
