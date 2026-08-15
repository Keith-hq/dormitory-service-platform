using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TemplateDormApi.Models;

/// <summary>
/// 个人分摊明细实体（映射 D_Fee_Detail）。
/// 表名/列名必须与 Oracle 实际存储的大小写一致（建表未加引号 → 全大写）：
/// Oracle 提供商会按映射原样加引号生成 SQL，混合大小写会导致 ORA-00942/ORA-00904
/// （2026-08-15 接口实测 STU-04 暴露后统一修正）。
/// </summary>
[Table("D_FEE_DETAIL", Schema = "DORM_OPER")]
public class FeeDetail
{
    [Key]
    [Column("DETAIL_ID")]
    public int DetailId { get; set; }

    [Column("FEE_ID")]
    public int FeeId { get; set; }

    [Column("STUDENT_ID")]
    public string StudentId { get; set; } = string.Empty;

    [Column("ROOM_ID")]
    public int RoomId { get; set; }

    [Column("WATER_SHARE")]
    public decimal WaterShare { get; set; }

    [Column("POWER_SHARE")]
    public decimal PowerShare { get; set; }

    [Column("STAY_DAYS")]
    public int StayDays { get; set; }

    [Column("TOTAL_DAYS")]
    public int TotalDays { get; set; }

    [Column("BILL_TYPE")]
    public string BillType { get; set; } = string.Empty;

    [Column("IS_PAID")]
    public string IsPaid { get; set; } = "否";

    [Column("CREATE_TIME")]
    public DateTime CreateTime { get; set; }
}
