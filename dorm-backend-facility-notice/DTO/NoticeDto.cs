namespace DormBackendFacilityNotice.DTO;

/// <summary>
/// 公告发布 DTO（骨架，字段待对照契约补全）
/// </summary>
public class NoticeCreateDto
{
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
}

/// <summary>
/// 公告编辑 DTO（骨架）
/// </summary>
public class NoticeUpdateDto
{
    public string? Title { get; set; }
    public string? Content { get; set; }
}
