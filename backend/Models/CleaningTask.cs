namespace TemplateDormApi.Models;

/// <summary>
/// 保洁任务实体，对应 D_Cleaning_Task（扩展表）。
/// 任务生成（SVC-SCHED-04 保洁生成）归属兰皓衍调度域；本模块仅实现 DORM-39/40
/// 列表与完成。
/// </summary>
public class CleaningTask
{
    public int TaskId { get; set; }

    public int FacilityId { get; set; }

    public int TriggerCount { get; set; }

    /// <summary>状态（待处理 / 已完成）</summary>
    public string Status { get; set; } = "待处理";

    public DateTime CreateTime { get; set; }

    public DateTime? CompleteTime { get; set; }
}
