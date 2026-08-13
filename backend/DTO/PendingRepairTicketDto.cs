namespace TemplateDormApi.DTO;

/// <summary>
/// 待处理/处理中报修工单列表项（DORM-26）。
/// 无键 DTO，经 SqlQueryRaw 直接投影——列别名（带引号）与属性名精确一致，
/// 分页由 SQL 内 OFFSET/FETCH 完成，避免 EF 对实体映射做二次组合（ORA-00904）。
/// </summary>
public class PendingRepairTicketDto
{
    public int TicketId { get; set; }
    public string? StudentId { get; set; }
    public int RoomId { get; set; }
    public string IssueDesc { get; set; } = string.Empty;
    public DateTime SubmitTime { get; set; }
    public string Status { get; set; } = string.Empty;
    public string SlaLevel { get; set; } = string.Empty;
    public DateTime? Deadline { get; set; }
    public string? AssignedTo { get; set; }
    public DateTime? EscalationTime { get; set; }
}
