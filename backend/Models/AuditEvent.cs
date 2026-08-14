using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TemplateDormApi.Models;

[Table("D_AUDIT_EVENT")]
public class AuditEvent
{
    [Column("AUDIT_ID")]
    public int AuditId { get; set; }

    [Column("ACTOR_ACCOUNT_ID")]
    public int? ActorAccountId { get; set; }

    [Column("EVENT_TYPE")]
    [MaxLength(50)]
    public string EventType { get; set; } = string.Empty;

    [Column("TARGET_TYPE")]
    [MaxLength(50)]
    public string? TargetType { get; set; }

    [Column("TARGET_ID")]
    [MaxLength(50)]
    public string? TargetId { get; set; }

    [Column("EVENT_TIME")]
    public DateTime EventTime { get; set; }
}