using Quartz;
using TemplateDormApi.Services;

namespace TemplateDormApi.Jobs;

/// <summary>
/// 共享物品逾期巡检——每15分钟扫描未归还的超期记录，给借款人发逾期提醒（难点④）。
/// 不扣信用分：超期归还按次扣 2 分由归还路径经信用分统一入口完成（PRD 规则）。
/// SP_Check_Overdue 按"同一笔借出同一天一条"去重提醒。
/// </summary>
[DisallowConcurrentExecution]
public class OverdueCheckJob : IJob
{
    private readonly IInventoryTxnService _service;
    public OverdueCheckJob(IInventoryTxnService service) => _service = service;
    public async Task Execute(IJobExecutionContext context) => await _service.CheckOverdue();
}
