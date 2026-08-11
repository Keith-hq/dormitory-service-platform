using System.ComponentModel.DataAnnotations;

namespace TemplateDormApi.DTO;

public sealed class LateEntryQueryDto
{
    [Range(1, int.MaxValue, ErrorMessage = "page 必须大于等于 1")]
    public int Page { get; set; } = 1;

    [Range(1, 100, ErrorMessage = "pageSize 必须在 1 到 100 之间")]
    public int PageSize { get; set; } = 10;
}

public sealed class UpdateLateEntryReasonRequest
{
    [Required(ErrorMessage = "晚归说明不能为空")]
    [StringLength(200, ErrorMessage = "晚归说明不能超过 200 个字符")]
    public string Reason { get; set; } = string.Empty;
}

public sealed class CreateLateEntryRequest
{
    [Required(ErrorMessage = "学号不能为空")]
    [StringLength(20, ErrorMessage = "学号不能超过 20 个字符")]
    public string StudentId { get; set; } = string.Empty;

    [Required(ErrorMessage = "晚归时间不能为空")]
    public DateTime RecordTime { get; set; }

    [StringLength(200, ErrorMessage = "晚归说明不能超过 200 个字符")]
    public string? Reason { get; set; }
}

public sealed class LateEntryDto
{
    public long RecordId { get; set; }
    public string StudentId { get; set; } = string.Empty;
    public DateTime RecordTime { get; set; }
    public string? Reason { get; set; }
}
