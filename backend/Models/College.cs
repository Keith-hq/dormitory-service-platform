using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TemplateDormApi.Models;

[Table("D_COLLEGE")]
public class College
{
    [Column("COLLEGE_ID")]
    public int CollegeId { get; set; }

    [Column("COLLEGE_NAME")]
    [MaxLength(100)]
    public string CollegeName { get; set; } = string.Empty;

    [Column("COUNSELOR_NAME")]
    [MaxLength(50)]
    public string? CounselorName { get; set; }

    [Column("CONTACT_PHONE")]
    [MaxLength(20)]
    public string? ContactPhone { get; set; }
}