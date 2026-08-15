using System.ComponentModel.DataAnnotations;

namespace TemplateDormApi.DTO;

/// <summary>
/// 发起投票请求（STU-29）
/// </summary>
public class CreateVoteRequest
{
    [Range(1, int.MaxValue, ErrorMessage = "房间不能为空")]
    public int RoomId { get; set; }

    [Required(ErrorMessage = "议题不能为空")]
    [StringLength(200, ErrorMessage = "议题最长 200 字符")]
    public string Topic { get; set; } = string.Empty;

    /// <summary>截止时间（可选，不传默认 3 天后；对齐 DDL Deadline）</summary>
    public DateTime? Deadline { get; set; }

    [Range(1, 99, ErrorMessage = "应参与人数必须在 1~99 之间")]
    public int EligibleCount { get; set; } = 4;
    public string? InitiatorStudentId { get; set; }
    public int DurationDays { get; set; } // 投票持续天数
}

/// <summary>
/// 参与投票请求（STU-30）
/// </summary>
public class SubmitVoteRequest
{
    [Required(ErrorMessage = "投票选项不能为空")]
    [RegularExpression("^(同意|不同意)$", ErrorMessage = "投票选项只能是 同意 或 不同意")]
    public string Choice { get; set; } = string.Empty;
}

/// <summary>
/// 投票统计结果（STU-31）
/// </summary>
public class VoteStatisticsDto
{
    public int VoteId { get; set; }
    public string Topic { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int EligibleCount { get; set; }
    public int AgreeCount { get; set; }
    public int DisagreeCount { get; set; }
    public int TotalCount { get; set; }
}
