using System.ComponentModel.DataAnnotations;

namespace TemplateDormApi.Models;

/// <summary>
/// 房间投票实体（映射 D_Room_Vote，主键由序列+触发器生成）
/// </summary>
public class RoomVote
{
    [Key]
    public int VoteId { get; set; }

    /// <summary>所属房间（FK → D_Room）</summary>
    public int RoomId { get; set; }

    /// <summary>发起学生（FK → D_Student）</summary>
    public string InitiatorStudentId { get; set; } = string.Empty;

    /// <summary>投票议题</summary>
    public string Topic { get; set; } = string.Empty;

    /// <summary>创建时间</summary>
    public DateTime CreateTime { get; set; }

    /// <summary>截止时间</summary>
    public DateTime Deadline { get; set; }

    /// <summary>应参与人数</summary>
    public int EligibleCount { get; set; }

    /// <summary>状态（进行中 / 已通过 / 未通过 / 已结束）</summary>
    public string Status { get; set; } = "进行中";
}
