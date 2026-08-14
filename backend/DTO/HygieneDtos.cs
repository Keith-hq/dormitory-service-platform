using System.ComponentModel.DataAnnotations;

namespace TemplateDormApi.DTO;

public sealed class CreateHygieneRecordRequest
{
    [Range(1, long.MaxValue, ErrorMessage = "房间 ID 必须大于 0")]
    public long RoomId { get; set; }

    [Range(0, 100, ErrorMessage = "卫生评分必须在 0 到 100 之间")]
    public int Score { get; set; }

    [StringLength(500, ErrorMessage = "评语不能超过 500 个字符")]
    public string? Comment { get; set; }
}

public sealed class UpdateHygieneRecordRequest
{
    [Range(0, 100, ErrorMessage = "卫生评分必须在 0 到 100 之间")]
    public int Score { get; set; }

    [StringLength(500, ErrorMessage = "评语不能超过 500 个字符")]
    public string? Comment { get; set; }
}

public sealed class HygieneRankingQueryDto
{
    [RegularExpression(@"^\d{4}-(0[1-9]|1[0-2])$", ErrorMessage = "yearMonth 格式必须为 yyyy-MM")]
    public string? YearMonth { get; set; }
}

public sealed class HygieneRecordDto
{
    public long RecordId { get; set; }
    public long RoomId { get; set; }
    public DateTime CheckDate { get; set; }
    public decimal Score { get; set; }
    public string? InspectorId { get; set; }
    public string? Comment { get; set; }
}

public sealed class HygieneRankingDto
{
    public long RoomId { get; set; }
    public decimal AverageScore { get; set; }
    public int Rank { get; set; }
}
