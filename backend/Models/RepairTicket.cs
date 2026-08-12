namespace TemplateDormApi.Models;

/// <summary>
/// 报修工单实体，对应 D_Repair_Ticket。
/// 学生报修=辛泓毅（STU-08~12），派单/SLA/接单/完工=李昂（难点⑤ DORM-26~28）。
/// </summary>
public class RepairTicket
{
    public int TicketId { get; set; }

    public string StudentId { get; set; } = string.Empty;

    public int RoomId { get; set; }

    public string IssueDesc { get; set; } = string.Empty;

    public DateTime SubmitTime { get; set; }

    public string Status { get; set; } = "待处理";

    /// <summary>SLA 级别：普通（24h）/ 紧急（12h）</summary>
    public string SlaLevel { get; set; } = "普通";

    /// <summary>最晚处理时间</summary>
    public DateTime? Deadline { get; set; }

    /// <summary>指派管理员 ID</summary>
    public string? AssignedTo { get; set; }
}
