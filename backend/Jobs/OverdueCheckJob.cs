using Quartz;
using TemplateDormApi.Services;

namespace TemplateDormApi.Jobs;

/// <summary>
/// 共享物品逾期巡检——每15分钟扫描未归还的超期记录并扣信用分（难点④）。
/// SP_Check_Overdue 内部通过 Event_Key 幂等：同一 Loan_ID 每天只扣一次。
/// </summary>
[DisallowConcurrentExecution]
public class OverdueCheckJob : IJob
{
    private readonly IInventoryTxnService _service;
    public OverdueCheckJob(IInventoryTxnService service) => _service = service;
    public async Task Execute(IJobExecutionContext context) => await _service.CheckOverdue();
}
