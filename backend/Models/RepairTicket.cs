namespace TemplateDormApi.Models;

/// <summary>
/// 报修工单，对应 D_REPAIR_TICKET。
/// 学生报修=辛泓毅（STU-08~12），派单/SLA/接单/完工=李昂（难点⑤ DORM-26~28）。
/// EscalationTime 为难点⑤ 迁移 021 新增列（SLA 首次升级标记）。
/// </summary>
public sealed class RepairTicket
{
    public long TicketId { get; set; }
    public string? StudentId { get; set; }
    public int? RoomId { get; set; }
    public string IssueDescription { get; set; } = string.Empty;
    public DateTime SubmitTime { get; set; }
    public string? Status { get; set; }
    public string SlaLevel { get; set; } = "普通";
    public DateTime? Deadline { get; set; }
    public string? AssignedTo { get; set; }

    /// <summary>SLA 首次升级时间（NULL=未升级，NOT NULL=已升级，防二次升级，难点⑤）</summary>
    public DateTime? EscalationTime { get; set; }

    /// <summary>接单时间（迁移 040；NULL=未接单，NOT NULL=已由指派维修员接单，UI 派生显示"已接收"）</summary>
    public DateTime? ClaimTime { get; set; }

    public RepairLog? Log { get; set; }
    public ICollection<RepairAttachment> Attachments { get; set; } = new List<RepairAttachment>();
}
