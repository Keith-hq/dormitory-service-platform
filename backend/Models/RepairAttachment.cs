namespace TemplateDormApi.Models;

public sealed class RepairAttachment
{
    public long AttachmentId { get; set; }
    public long TicketId { get; set; }
    public string StorageRef { get; set; } = string.Empty;
    public string? OriginalName { get; set; }
    public string? ContentType { get; set; }
    public long? FileSize { get; set; }
    public DateTime CreateTime { get; set; }
    public RepairTicket? Ticket { get; set; }
}
