namespace TemplateDormApi.Models;

/// <summary>
/// 学生晚归记录，对应 D_LATE_ENTRY。
/// </summary>
public sealed class LateEntry
{
    public long RecordId { get; set; }
    public string? StudentId { get; set; }
    public DateTime ReturnTime { get; set; }
    public string? Reason { get; set; }
}
