using TemplateDormApi.Models;

namespace TemplateDormApi.Services;

/// <summary>
/// SLA 派单服务接口（难点⑤）。
/// 覆盖 DORM-26（待处理列表）、DORM-27（接单）、DORM-28（完工）、SLA 升级巡检。
/// </summary>
public interface ISlaDispatchService
{
    /// <summary>自动派单：查楼栋→维修员→设置 Assigned_To/SLA_Level/Deadline</summary>
    Task<int> AssignTicket(int ticketId);

    /// <summary>管理员接单（并发唯一）</summary>
    Task<int> ClaimTicket(int ticketId, string adminId);

    /// <summary>管理员完成维修 + 写日志</summary>
    Task<int> CompleteRepair(int ticketId, string adminId, string processDesc);

    /// <summary>SLA 升级巡检</summary>
    Task EscalateSla();

    /// <summary>查询当前管理员的待处理工单（DORM-26）</summary>
    Task<List<RepairTicket>> GetPendingTickets(string adminId);
}
