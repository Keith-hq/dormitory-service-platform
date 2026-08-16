using TemplateDormApi.DTO;

namespace TemplateDormApi.Services;

/// <summary>保洁任务（DORM-39/40；任务生成 SVC-SCHED-04 属兰皓衍调度域）</summary>
public interface ICleaningTaskService
{
    Task<PagedResult<CleaningTaskDto>> GetPagedAsync(int page, int pageSize, CancellationToken cancellationToken);

    Task<CleaningTaskDto> CompleteAsync(int taskId, CancellationToken cancellationToken);
}
