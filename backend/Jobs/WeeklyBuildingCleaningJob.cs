using Quartz;
using TemplateDormApi.Services;

namespace TemplateDormApi.Jobs;

/// <summary>
/// 每周楼栋整体保洁（SVC-SCHED-04 保洁生成·楼栋）：每周一 06:00 执行，
/// 按“楼长负责的楼栋”各生成一条楼栋整体保洁任务。
/// </summary>
public sealed class WeeklyBuildingCleaningJob : IJob
{
    private readonly ICleaningRequestService _service;

    public WeeklyBuildingCleaningJob(ICleaningRequestService service)
    {
        _service = service;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        await _service.GenerateBuildingWeeklyAsync(context.CancellationToken);
    }
}
