namespace TemplateDormApi.Models;

public sealed class FacilityBooking
{
    public long BookingId { get; set; }
    public int FacilityId { get; set; }
    public string StudentId { get; set; } = string.Empty;
    public DateTime CreateTime { get; set; }
    public DateTime? StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public string Status { get; set; } = string.Empty;
}
