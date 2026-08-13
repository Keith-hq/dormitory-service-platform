namespace TemplateDormApi.Models;

public sealed class UtilityFee
{
    public long FeeId { get; set; }
    public long RoomId { get; set; }
    public string YearMonth { get; set; } = string.Empty;
    public decimal? WaterFee { get; set; }
    public decimal? PowerFee { get; set; }
    public string? IsPaid { get; set; }
    public string PublishStatus { get; set; } = string.Empty;
}
