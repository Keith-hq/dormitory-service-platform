using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TemplateDormApi.Models
{
    [Table("D_Visitor_Authorization")]
    public class VisitorAuthorization
    {
        [Key]
        [Column("Authorization_ID")]
        public long AuthorizationId { get; set; }

        [Column("Student_ID")]
        public string StudentId { get; set; }

        [Column("Room_ID")]
        public long RoomId { get; set; }

        [Column("Visitor_Name")]
        public string VisitorName { get; set; }

        [Column("Visit_Reason")]
        public string? VisitReason { get; set; }

        [Column("Authorization_Token")]
        public string AuthorizationToken { get; set; }

        [Column("Expires_Time")]
        public DateTime ExpiresTime { get; set; }

        [Column("Status")]
        public string Status { get; set; } = "有效";

        [Column("Create_Time")]
        public DateTime CreateTime { get; set; } = DateTime.Now;
    }
}