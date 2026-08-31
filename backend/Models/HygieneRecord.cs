namespace TemplateDormApi.Models;

/// <summary>
/// 房间卫生评分，对应 D_HYGIENE_RECORD。
/// </summary>
public sealed class HygieneRecord
{
    public long RecordId { get; set; }
    public int? RoomId { get; set; }
    public DateTime CheckDate { get; set; }
    public decimal Score { get; set; }
    public string? InspectorId { get; set; }

    /// <summary>评语（可空）。041 起自 D_Hygiene_Comment 并入本表，
    /// 列名为带引号关键字标识符 "COMMENT"。</summary>
    public string? CommentText { get; set; }
}
