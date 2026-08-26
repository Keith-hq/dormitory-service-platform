using System.ComponentModel.DataAnnotations;

namespace TemplateDormApi.DTO;

public sealed class CreateViolationRequest
{
    [Required(ErrorMessage = "学号不能为空")]
    [StringLength(20, ErrorMessage = "学号不能超过 20 个字符")]
    public string StudentId { get; set; } = string.Empty;

    [Required(ErrorMessage = "违规类型不能为空")]
    [RegularExpression("^(查寝未归|违章电器|其他)$", ErrorMessage = "违规类型不合法")]
    public string Type { get; set; } = string.Empty;

    public string? Detail { get; set; }
}

public sealed class ViolationQueryDto
{
    public long? BuildingId { get; set; }
    public string? StudentId { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "page 必须大于等于 1")]
    public int Page { get; set; } = 1;

    [Range(1, 100, ErrorMessage = "pageSize 必须在 1 到 100 之间")]
    public int PageSize { get; set; } = 10;
}

public sealed class ViolationDto
{
    public long ViolationId { get; set; }
    public string StudentId { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string? Detail { get; set; }
    public DateTime RecordTime { get; set; }
    public string? RecordBy { get; set; }
    public string Status { get; set; } = "有效";
}
