using Microsoft.Extensions.Logging;
using Quartz;
using TemplateDormApi.Services;

namespace TemplateDormApi.Jobs;

/// <summary>
/// 自动扣款任务：每月1/2/3日执行，attemptNo 由调度参数传入
/// </summary>
public class AutoDeductJob : IJob
{
    private readonly IBillingService _service;
    private readonly ILogger<AutoDeductJob> _logger;

    public AutoDeductJob(IBillingService service, ILogger<AutoDeductJob> logger)
    {
        _service = service;
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        try
        {
            var dataMap = context.MergedJobDataMap;
            int attemptNo = dataMap.GetInt("attemptNo");
            var yearMonth = DateTime.Now.ToString("yyyy-MM");
            await _service.AutoDeduct(attemptNo, yearMonth);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "AutoDeductJob 执行失败（SP 可能尚未部署）");
        }
    }
}

/// <summary>
/// 断电判定任务：每月3日凌晨执行（第三次扣款之后）
/// </summary>
public class PowerCutJob : IJob
{
    private readonly IBillingService _service;
    private readonly ILogger<PowerCutJob> _logger;

    public PowerCutJob(IBillingService service, ILogger<PowerCutJob> logger)
    {
        _service = service;
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        try
        {
            var yearMonth = DateTime.Now.ToString("yyyy-MM");
            await _service.CheckPowerCut(yearMonth);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "PowerCutJob 执行失败（SP 可能尚未部署）");
        }
    }
}

/// <summary>
/// 恢复供电巡检：每分钟执行
/// </summary>
public class RestorePowerJob : IJob
{
    private readonly IBillingService _service;
    private readonly ILogger<RestorePowerJob> _logger;

    public RestorePowerJob(IBillingService service, ILogger<RestorePowerJob> logger)
    {
        _service = service;
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        try
        {
            await _service.RestorePower();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "RestorePowerJob 执行失败（SP 可能尚未部署）");
        }
    }
}
