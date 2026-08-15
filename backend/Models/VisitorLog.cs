using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TemplateDormApi.Models;

[Table("D_VISITOR_LOG")]
public class VisitorLog
{
    [Column("VISITOR_ID")]
    public int VisitorId { get; set; }

    [Column("BUILDING_ID")]
    public int? BuildingId { get; set; }

    [Column("VISITOR_NAME")]
    [MaxLength(50)]
    public string VisitorName { get; set; } = string.Empty;

    [Column("VISIT_REASON")]
    [MaxLength(200)]
    public string VisitReason { get; set; } = string.Empty;

    [Column("ENTRY_TIME")]
    public DateTime EntryTime { get; set; }

    [Column("LEAVE_TIME")]
    public DateTime? LeaveTime { get; set; }
}