using TemplateDormApi.DTO;
using TemplateDormApi.Models;

namespace TemplateDormApi.Services;

/// <summary>
/// SLA 派单服务接口（难点⑤ 一审修复版）。
/// 覆盖 DORM-26（待处理列表·分页）、DORM-27（接单）、DORM-28（完工写日志）、SLA 升级巡检。
/// </summary>
public interface ISlaDispatchService
{
    /// <summary>自动派单：查楼栋→维修员→设置 Assigned_To/SLA_Level/Deadline</summary>
    Task<int> AssignTicket(int ticketId);

    /// <summary>被指派的维修员接单（并发唯一，仅允许 Assigned_To 匹配）</summary>
    Task<int> ClaimTicket(int ticketId, string adminId);

    /// <summary>管理员完成维修 + 写日志（需校验 Status='处理中' AND Assigned_To=本人）</summary>
    Task<int> CompleteRepair(int ticketId, string adminId, string content, string? result, DateTime? solveTime);

    /// <summary>SLA 升级巡检（只提醒不转派）</summary>
    Task EscalateSla();

    /// <summary>查询当前管理员的待处理/处理中工单（DORM-26，分页）</summary>
    Task<PagedResult<RepairTicket>> GetPendingTickets(string adminId, int page, int pageSize);
}
