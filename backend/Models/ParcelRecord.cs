using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TemplateDormApi.Models;

[Table("D_PARCEL_RECORD")]
public class ParcelRecord
{
    [Column("PARCEL_ID")]
    public int ParcelId { get; set; }

    [Column("STUDENT_ID")]
    [MaxLength(20)]
    public string? StudentId { get; set; }

    [Column("ARRIVE_TIME")]
    public DateTime ArriveTime { get; set; }

    [Column("PICKUP_TIME")]
    public DateTime? PickupTime { get; set; }

    [Column("COURIER_COMPANY")]
    [MaxLength(50)]
    public string? CourierCompany { get; set; }
}