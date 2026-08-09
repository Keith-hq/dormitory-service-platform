using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TemplateDormApi.Models;

[Table("D_Room_Vote")]
public class RoomVote
{
    [Key]
    [Column("Vote_ID")]
    public long VoteId { get; set; }

    [Column("Initiator_Student_ID")]
    public string InitiatorStudentId { get; set; }

    [Column("Topic")]
    public string Topic { get; set; }

    [Column("Create_Time")]
    public DateTime CreateTime { get; set; } = DateTime.Now;

    [Column("Deadline")]
    public DateTime Deadline { get; set; }

    [Column("Eligible_Count")]
    public int EligibleCount { get; set; }

    [Column("Status")]
    public string Status { get; set; } = "进行中";
}