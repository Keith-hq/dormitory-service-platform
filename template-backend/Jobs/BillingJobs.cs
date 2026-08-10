using Quartz;
using TemplateDormApi.Services;

namespace TemplateDormApi.Jobs;

/// <summary>
/// 自动扣款任务：每月1/2/3日执行，attemptNo 由调度参数传入
/// </summary>
public class AutoDeductJob : IJob
{
    private readonly IBillingService _service;

    public AutoDeductJob(IBillingService service)
    {
        _service = service;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        // attemptNo 从 JobDataMap 读取，调度时注入 1/2/3
        var dataMap = context.MergedJobDataMap;
        int attemptNo = dataMap.GetInt("attemptNo");
        var yearMonth = DateTime.Now.ToString("yyyy-MM");

        await _service.AutoDeduct(attemptNo, yearMonth);
    }
}

/// <summary>
/// 断电判定任务：每月3日凌晨执行（第三次扣款之后）
/// </summary>
public class PowerCutJob : IJob
{
    private readonly IBillingService _service;

    public PowerCutJob(IBillingService service)
    {
        _service = service;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        var yearMonth = DateTime.Now.ToString("yyyy-MM");
        await _service.CheckPowerCut(yearMonth);
    }
}

/// <summary>
/// 恢复供电巡检：每分钟执行
/// </summary>
public class RestorePowerJob : IJob
{
    private readonly IBillingService _service;

    public RestorePowerJob(IBillingService service)
    {
        _service = service;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        await _service.RestorePower();
    }
}
