using System.ComponentModel.DataAnnotations;

namespace TemplateDormApi.DTO;

public sealed class CreateVisitorRegistryRequest
{
    [Required(ErrorMessage = "访客姓名不能为空")]
    [StringLength(50, ErrorMessage = "访客姓名不能超过 50 个字符")]
    public string VisitorName { get; set; } = string.Empty;

    [StringLength(20, ErrorMessage = "联系电话不能超过 20 个字符")]
    public string? Phone { get; set; }

    [StringLength(20, ErrorMessage = "学号不能超过 20 个字符")]
    public string? StudentId { get; set; }
}

public sealed class VerifyVisitorRegistryRequest
{
    [Required(ErrorMessage = "二维码令牌不能为空")]
    public string QrToken { get; set; } = string.Empty;
}

public sealed class VisitorRegistryDto
{
    public long RegistryId { get; set; }
    public string VisitorName { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? StudentId { get; set; }
    public DateTime EntryTime { get; set; }
    public DateTime? ExitTime { get; set; }
    public string Status { get; set; } = string.Empty;
}
