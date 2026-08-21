using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TemplateDormApi.Models;

[Table("D_ACCESS_LOG")]
public class AccessLog
{
    [Column("LOG_ID")]
    public int LogId { get; set; }

    [Column("STUDENT_ID")]
    [MaxLength(20)]
    public string? StudentId { get; set; }

    [Column("BUILDING_ID")]
    public int? BuildingId { get; set; }

    [Column("SWIPE_TIME")]
    public DateTime SwipeTime { get; set; }

    [Column("DIRECTION")]
    [MaxLength(10)]
    public string Direction { get; set; } = string.Empty;
}