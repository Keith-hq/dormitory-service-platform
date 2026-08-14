using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TemplateDormApi.Models;

[Table("D_MAJOR")]
public class Major
{
    [Column("MAJOR_ID")]
    public int MajorId { get; set; }

    [Column("COLLEGE_ID")]
    public int? CollegeId { get; set; }

    [Column("MAJOR_NAME")]
    [MaxLength(100)]
    public string MajorName { get; set; } = string.Empty;

    public College? College { get; set; }
}