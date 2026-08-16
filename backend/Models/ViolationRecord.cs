using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TemplateDormApi.Models;

[Table("D_VIOLATION_RECORD")]
public class ViolationRecord
{
    [Column("RECORD_ID")]
    public int RecordId { get; set; }

    [Column("STUDENT_ID")]
    [MaxLength(20)]
    public string? StudentId { get; set; }

    [Column("ROOM_ID")]
    public int? RoomId { get; set; }

    [Column("VIO_TYPE")]
    [MaxLength(50)]
    public string VioType { get; set; } = string.Empty;

    [Column("VIO_DATE")]
    public DateTime VioDate { get; set; }

    [Column("PENALTY")]
    [MaxLength(100)]
    public string? Penalty { get; set; }
}
