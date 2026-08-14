namespace TemplateDormApi.Models;

public sealed class RepairLog
{
    public long LogId { get; set; }
    public long? TicketId { get; set; }
    public string? AdminId { get; set; }
    public string? ProcessDescription { get; set; }
    public DateTime ResolveTime { get; set; }
}
