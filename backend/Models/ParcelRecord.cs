using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TemplateDormApi.Models;

/// <summary>
/// 快递代收记录（映射 D_Parcel_Record）——退宿三步校验的只读数据源。
/// 写入方为快递模块（C8 链路），本模块不写此表（跨模块只读，符合单写者红线）。
/// </summary>
public class ParcelRecord
{
    [Column("PARCEL_ID")]
    public int ParcelId { get; set; }

    [Column("STUDENT_ID")]
    [MaxLength(20)]
    public string? StudentId { get; set; }

    [Column("ARRIVE_TIME")]
    public DateTime ArriveTime { get; set; }

    /// <summary>取件时间；NULL = 未取（退宿校验不通过项）</summary>
    public DateTime? PickupTime { get; set; }

    [Column("COURIER_COMPANY")]
    [MaxLength(50)]
    public string? CourierCompany { get; set; }

    public Student? Student { get; set; }
}
