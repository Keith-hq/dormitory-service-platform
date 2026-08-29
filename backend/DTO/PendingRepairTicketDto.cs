namespace TemplateDormApi.DTO;

/// <summary>
/// 待处理/已派单报修工单列表项（DORM-26）。
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
    public DateTime? ClaimTime { get; set; }

    /// <summary>报修位置（楼栋名 + 房间号，如"男生宿舍楼 900101"），JOIN D_Room/D_Building 得出</summary>
    public string? Location { get; set; }

    /// <summary>附件引用列表：每条 "原文件名|存储ref"，用 ; 分隔（LISTAGG 聚合），供维修端展示</summary>
    public string? AttachmentRefs { get; set; }
}
