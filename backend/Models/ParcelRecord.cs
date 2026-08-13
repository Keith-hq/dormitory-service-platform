using System.ComponentModel.DataAnnotations;

namespace TemplateDormApi.Models;

/// <summary>
/// 快递记录实体（映射 D_Parcel_Record，主键由序列+触发器生成）。
/// 取件状态由 Pickup_Time 是否为空判断：NULL=未取件，非空=已取件。
/// </summary>
public class ParcelRecord
{
    [Key]
    public int ParcelId { get; set; }

    /// <summary>收件学生（FK → D_Student，可空）</summary>
    public string? StudentId { get; set; }

    /// <summary>到达时间</summary>
    public DateTime ArriveTime { get; set; }

    /// <summary>取件时间（NULL=未取）</summary>
    public DateTime? PickupTime { get; set; }

    /// <summary>快递公司</summary>
    public string? CourierCompany { get; set; }
}
