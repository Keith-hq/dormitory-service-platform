using Microsoft.EntityFrameworkCore;
using Quartz;
using TemplateDormApi.Data;
using TemplateDormApi.Services;

namespace TemplateDormApi.Jobs;

/// <summary>
/// 未指派工单补齐——每 30 秒扫描 Assigned_To IS NULL 的工单并自动派单（难点⑤）。
/// 正常流程由学生报修端在创建工单后直接调用 SP_Assign_Ticket；
/// 本 Job 为兜底：防止直接 INSERT 工单或调用失败导致漏派。
/// </summary>
[DisallowConcurrentExecution]
public class TicketAssignJob : IJob
{
    private readonly ISlaDispatchService _service;
    private readonly AppDbContext _context;

    public TicketAssignJob(ISlaDispatchService service, AppDbContext context)
    {
        _service = service;
        _context = context;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        // 标量投影（输出列别名为 "Value"），避免实体映射被 EF 二次组合（ORA-00904）
        var unassigned = await _context.Database
            .SqlQueryRaw<int>(@"SELECT Ticket_ID AS ""Value"" FROM D_Repair_Ticket
                                WHERE Assigned_To IS NULL")
            .ToListAsync();

        foreach (var ticketId in unassigned)
        {
            await _service.AssignTicket(ticketId);
        }
    }
}

/// <summary>
/// SLA 升级巡检——每 15 分钟扫描普通超时工单（难点⑤）。
/// 只提醒不转派：SP 原子标记 Escalation_Time 并返回新升级工单，通知楼长由公共服务投递；
/// 多实例并发时每单只会升级/通知一次（条件 UPDATE + SQL%ROWCOUNT 守门）。
/// </summary>
[DisallowConcurrentExecution]
public class SlaEscalationJob : IJob
{
    private readonly ISlaDispatchService _service;
    public SlaEscalationJob(ISlaDispatchService service) => _service = service;
    public async Task Execute(IJobExecutionContext context) => await _service.EscalateSla();
}
