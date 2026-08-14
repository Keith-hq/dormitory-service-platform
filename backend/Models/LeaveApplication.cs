using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TemplateDormApi.Models;

[Table("D_LEAVE_APPLICATION")]
public class LeaveApplication
{
    [Column("APPLY_ID")]
    public int ApplyId { get; set; }

    [Column("STUDENT_ID")]
    [MaxLength(20)]
    public string? StudentId { get; set; }

    [Column("LEAVE_DATE")]
    public DateTime LeaveDate { get; set; }

    [Column("RETURN_DATE")]
    public DateTime ReturnDate { get; set; }

    [Column("DESTINATION")]
    [MaxLength(200)]
    public string Destination { get; set; } = string.Empty;

    [Column("STATUS")]
    [MaxLength(20)]
    public string Status { get; set; } = "待批";
}