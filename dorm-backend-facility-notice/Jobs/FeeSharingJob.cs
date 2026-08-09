using Quartz;
using DormBackendFacilityNotice.Services;

namespace DormBackendFacilityNotice.Jobs;

/// <summary>
/// 水电分摊定时任务：每月1日凌晨0点执行
/// </summary>
public class FeeSharingJob : IJob
{
    private readonly IFeeSharingService _service;

    public FeeSharingJob(IFeeSharingService service)
    {
        _service = service;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        // 获取当前月份，格式 2026-08
        var yearMonth = DateTime.Now.ToString("yyyy-MM");

        await _service.CalcMonthlyFee(yearMonth);
    }
}
