using TemplateDormApi.DTO;

namespace TemplateDormApi.Services;

/// <summary>
/// SLA 派单服务接口（难点⑤ 三审修复版）。
/// 覆盖 DORM-26（待处理列表·分页）、DORM-27（接单）、DORM-28（完工写日志）、SLA 升级巡检。
/// </summary>
public interface ISlaDispatchService
{
    /// <summary>自动派单：查楼栋→维修员→设置 Assigned_To/SLA_Level/Deadline</summary>
    Task<int> AssignTicket(int ticketId);

    /// <summary>被指派的维修员接单（并发唯一，仅允许 Assigned_To 匹配）</summary>
    Task<int> ClaimTicket(int ticketId, string adminId);

    /// <summary>
    /// 管理员完成维修 + 写日志（需校验 Status='处理中' AND Assigned_To=本人）。
    /// repairResult 为 DORM-28 契约字段（已修复/需更换配件/无法修复），可为 NULL。
    /// </summary>
    Task<int> CompleteRepair(int ticketId, string adminId, string content, string? repairResult, DateTime? solveTime);

    /// <summary>
    /// SLA 升级巡检（只提醒不转派）：
    /// SP 原子标记 Escalation_Time 并返回本次新升级工单，通知由公共服务 INotificationService 投递。
    /// </summary>
    Task EscalateSla();

    /// <summary>查询当前管理员的待处理/处理中工单（DORM-26，分页，DTO 投影）</summary>
    Task<PagedResult<PendingRepairTicketDto>> GetPendingTickets(string adminId, int page, int pageSize);
}
