using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace TemplateDormApi.DTO;

public sealed class SubmitRepairTicketRequest
{
    [Required(ErrorMessage = "报修描述不能为空")]
    [StringLength(500, MinimumLength = 10, ErrorMessage = "报修描述长度必须在 10 到 500 个字符之间")]
    public string Description { get; set; } = string.Empty;

    [Required(ErrorMessage = "紧急程度不能为空")]
    [RegularExpression("^(普通|紧急)$", ErrorMessage = "紧急程度只能为普通或紧急")]
    public string Urgency { get; set; } = string.Empty;

    public int? RoomId { get; set; }
}

public sealed class RepairTicketQueryDto
{
    [Range(1, int.MaxValue, ErrorMessage = "page 必须大于等于 1")]
    public int Page { get; set; } = 1;

    [Range(1, 100, ErrorMessage = "pageSize 必须在 1 到 100 之间")]
    public int PageSize { get; set; } = 10;
}

public sealed class UploadRepairAttachmentsRequest
{
    [Required(ErrorMessage = "至少上传一个文件")]
    [MinLength(1, ErrorMessage = "至少上传一个文件")]
    public List<IFormFile> Files { get; set; } = new();
}

public sealed class RepairTicketDto
{
    public long TicketId { get; set; }
    public string StudentId { get; set; } = string.Empty;
    public int RoomId { get; set; }
    public string Description { get; set; } = string.Empty;
    public DateTime SubmitTime { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? SlaLevel { get; set; }
    public DateTime? Deadline { get; set; }
    public string? AssignedTo { get; set; }
    public RepairLogDto? Log { get; set; }
    public IReadOnlyList<RepairAttachmentDto> Attachments { get; set; } = Array.Empty<RepairAttachmentDto>();
}

public sealed class RepairLogDto
{
    public string? AdminId { get; set; }
    public string? ProcessDescription { get; set; }
    public DateTime ResolveTime { get; set; }
}

public sealed class RepairAttachmentDto
{
    public long AttachmentId { get; set; }
    public long TicketId { get; set; }
    public string OriginalName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public string StorageRef { get; set; } = string.Empty;
    public DateTime CreateTime { get; set; }
}
