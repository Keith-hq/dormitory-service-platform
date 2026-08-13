using Quartz;
using TemplateDormApi.Services;

namespace TemplateDormApi.Jobs;

/// <summary>
/// 共享物品逾期巡检——每15分钟执行（难点④）：
/// 1. 逾期提醒：读取逾期候选 → 调通知公共服务（借出行锁互斥 + 同事务检查插入，
///    同一天每笔只提醒一次），通知失败只记录；
/// 2. 自愈补扣：对"已归还且逾期但无 OVERDUE-{Loan_ID} 流水"的借出重试扣分，
///    归还时信用服务临时失败留下的待补偿项在此闭环。
/// 不主动扣信用分：超期归还按次扣 2 分由归还路径经信用分统一入口完成（PRD 规则）。
/// </summary>
[DisallowConcurrentExecution]
public class OverdueCheckJob : IJob
{
    private readonly IInventoryTxnService _service;
    public OverdueCheckJob(IInventoryTxnService service) => _service = service;
    public async Task Execute(IJobExecutionContext context) => await _service.CheckOverdue();
}
