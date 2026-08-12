namespace TemplateDormApi.Models;

/// <summary>
/// 报修工单，对应 D_REPAIR_TICKET。
/// </summary>
public sealed class RepairTicket
{
    public long TicketId { get; set; }
    public string? StudentId { get; set; }
    public long? RoomId { get; set; }
    public string IssueDescription { get; set; } = string.Empty;
    public DateTime SubmitTime { get; set; }
    public string? Status { get; set; }
    public string SlaLevel { get; set; } = "普通";
    public DateTime? Deadline { get; set; }
    public string? AssignedTo { get; set; }
}
