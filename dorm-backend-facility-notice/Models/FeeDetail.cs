using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DormBackendFacilityNotice.Models;

/// <summary>
/// 个人分摊明细实体（映射 D_Fee_Detail）
/// </summary>
[Table("D_Fee_Detail", Schema = "DORM_OPER")]
public class FeeDetail
{
    [Key]
    [Column("Detail_ID")]
    public int DetailId { get; set; }

    [Column("Fee_ID")]
    public int FeeId { get; set; }

    [Column("Student_ID")]
    public string StudentId { get; set; } = string.Empty;

    [Column("Room_ID")]
    public int RoomId { get; set; }

    [Column("Water_Share")]
    public decimal WaterShare { get; set; }

    [Column("Power_Share")]
    public decimal PowerShare { get; set; }

    [Column("Stay_Days")]
    public int StayDays { get; set; }

    [Column("Total_Days")]
    public int TotalDays { get; set; }

    [Column("Bill_Type")]
    public string BillType { get; set; } = string.Empty;

    [Column("Is_Paid")]
    public string IsPaid { get; set; } = "否";

    [Column("Create_Time")]
    public DateTime CreateTime { get; set; }
}
