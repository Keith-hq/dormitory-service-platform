namespace TemplateDormApi.Models;

/// <summary>
/// 房间投票响应实体（映射 D_Room_Vote_Response）。
/// 复合主键（Vote_ID + Student_ID）天然保证一人一票。
/// </summary>
public class RoomVoteResponse
{
    /// <summary>投票 ID（复合主键之一，FK → D_Room_Vote）</summary>
    public int VoteId { get; set; }

    /// <summary>投票学生 ID（复合主键之一，FK → D_Student）</summary>
    public string StudentId { get; set; } = string.Empty;

    /// <summary>选择（同意 / 不同意）</summary>
    public string Choice { get; set; } = string.Empty;

    /// <summary>投票时间</summary>
    public DateTime VoteTime { get; set; }
}
