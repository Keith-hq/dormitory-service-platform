using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TemplateDormApi.Models;

[Table("D_WATER_ORDER")]
public class WaterOrder
{
    [Column("ORDER_ID")]
    public int OrderId { get; set; }

    [Column("ROOM_ID")]
    public int? RoomId { get; set; }

    [Column("ORDER_TIME")]
    public DateTime OrderTime { get; set; }

    [Column("QUANTITY")]
    public int Quantity { get; set; } = 1;

    [Column("STATUS")]
    [MaxLength(20)]
    public string Status { get; set; } = "未送达";
}