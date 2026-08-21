using System.ComponentModel.DataAnnotations;

namespace TemplateDormApi.DTO;

/// <summary>保洁任务响应（DORM-39）</summary>
public sealed class CleaningTaskDto
{
    public int TaskId { get; set; }
    public int FacilityId { get; set; }

    /// <summary>设施编号（如 WASHER-01），联查 D_Facility 展示用</summary>
    public string? FacilityCode { get; set; }

    public int TriggerCount { get; set; }
    public string Status { get; set; } = "待处理";
    public DateTime CreateTime { get; set; }
    public DateTime? CompleteTime { get; set; }
}
