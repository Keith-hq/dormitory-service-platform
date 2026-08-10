using Quartz;
using TemplateDormApi.Services;

namespace TemplateDormApi.Jobs;

/// <summary>
/// 信用分月度重置定时任务：每月1日凌晨0点10分执行。
/// </summary>
[DisallowConcurrentExecution]
public class CreditResetJob : IJob
{
    private readonly ICreditService _creditService;
    private readonly ILogger<CreditResetJob> _logger;

    public CreditResetJob(
        ICreditService creditService,
        ILogger<CreditResetJob> logger)
    {
        _creditService = creditService;
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
        => await ExecuteAsync(context.CancellationToken);

    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        var now = DateTime.Now;
        var result = await _creditService.ResetMonthlyAsync(
            now.Year,
            now.Month,
            cancellationToken);

        _logger.LogInformation(
            "信用分月度重置完成：{Year}-{Month}，Processed={Processed}，Skipped={Skipped}，Failed={Failed}",
            now.Year,
            now.Month,
            result.Processed,
            result.Skipped,
            result.Failed);

        if (result.Failed > 0)
        {
            _logger.LogError(
                "信用分月度重置存在失败项：{Year}-{Month}，Failed={Failed}，依赖重跑（幂等）处理",
                now.Year,
                now.Month,
                result.Failed);
        }
    }
}
