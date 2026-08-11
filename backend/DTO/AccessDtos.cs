using System.ComponentModel.DataAnnotations;

namespace TemplateDormApi.DTO;

public sealed class AccessLogQueryDto
{
    public long? BuildingId { get; set; }
    public string? StudentId { get; set; }
    public DateTime? StartTime { get; set; }
    public DateTime? EndTime { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "page 必须大于等于 1")]
    public int Page { get; set; } = 1;

    [Range(1, 100, ErrorMessage = "pageSize 必须在 1 到 100 之间")]
    public int PageSize { get; set; } = 10;
}

public sealed class AccessDensityQueryDto
{
    public long? BuildingId { get; set; }
}

public sealed class AccessLogDto
{
    public long LogId { get; set; }
    public string StudentId { get; set; } = string.Empty;
    public long BuildingId { get; set; }
    public long? RoomId { get; set; }
    public string Direction { get; set; } = string.Empty;
    public DateTime AccessTime { get; set; }
}

public sealed class AccessDensityDto
{
    public long BuildingId { get; set; }
    public int OnlineCount { get; set; }
    public decimal Density { get; set; }
}
