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
        // 查所有未指派工单
        var unassigned = await _context.Set<Models.RepairTicket>()
            .FromSqlRaw(
                @"SELECT Ticket_ID AS TicketId FROM D_Repair_Ticket
                  WHERE Assigned_To IS NULL")
            .Select(t => t.TicketId)
            .ToListAsync();

        foreach (var ticketId in unassigned)
        {
            await _service.AssignTicket(ticketId);
        }
    }
}

/// <summary>
/// SLA 升级巡检——每 15 分钟扫描普通超时工单并升级（难点⑤）。
/// 只升级"普通"超时工单 → 紧急 + 转派楼长 + Deadline 重置 12h；
/// "紧急"已超时的不再升级。
/// </summary>
[DisallowConcurrentExecution]
public class SlaEscalationJob : IJob
{
    private readonly ISlaDispatchService _service;
    public SlaEscalationJob(ISlaDispatchService service) => _service = service;
    public async Task Execute(IJobExecutionContext context) => await _service.EscalateSla();
}
