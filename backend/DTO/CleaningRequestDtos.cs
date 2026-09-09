namespace TemplateDormApi.DTO;

/// <summary>学生申请保洁请求体（可选备注）</summary>
public sealed class ApplyCleaningRequest
{
    public string? Reason { get; set; }
}

/// <summary>保洁任务 DTO（借道 D_Repair_Ticket；taskId = RepairTicket.TicketId）</summary>
public sealed class CleaningRequestDto
{
    public long TaskId { get; set; }

    /// <summary>来源：宿舍申请 / 楼栋整体 / 设施触发</summary>
    public string SourceType { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;
    public string Status { get; set; } = "待处理";

    public int? RoomId { get; set; }
    public string? RoomNumber { get; set; }

    public int? BuildingId { get; set; }
    public string? BuildingName { get; set; }

    public string? RequesterStudentId { get; set; }
    public string? RequesterName { get; set; }

    public string? AssigneeAdminId { get; set; }
    public string? AssigneeName { get; set; }

    public DateTime CreateTime { get; set; }
    public DateTime? CompleteTime { get; set; }
}
