using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TemplateDormApi.Models;

[Table("D_Room_Vote")]
public class RoomVote
{
    [Key]
    [Column("Vote_ID")]
    public long VoteId { get; set; }

    [Column("Room_ID")]
    public long RoomId { get; set; }

    [Column("Title")]
    public string Title { get; set; }

    [Column("Status")]
    public string Status { get; set; } = "进行中"; // 进行中、已结束
}