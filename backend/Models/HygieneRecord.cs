namespace TemplateDormApi.Models;

/// <summary>
/// 房间卫生评分，对应 D_HYGIENE_RECORD。
/// </summary>
public sealed class HygieneRecord
{
    public long RecordId { get; set; }
    public long? RoomId { get; set; }
    public DateTime CheckDate { get; set; }
    public decimal Score { get; set; }
    public string? InspectorId { get; set; }
    public HygieneComment? Comment { get; set; }
}
