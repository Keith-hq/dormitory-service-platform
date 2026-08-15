using TemplateDormApi.DTO;
using TemplateDormApi.Models;

namespace TemplateDormApi.Services;

/// <summary>
/// 离校报备状态词表（D_Leave_Application 无 CHECK 约束，状态机在本服务内强制）。
/// 契约（IT-C2-007）：通过→已通过；驳回→已驳回（必填原因）；撤销→已撤回；修改/撤销仅待批可用。
/// </summary>
public static class LeaveStatuses
{
    public const string Pending = "待批";
    public const string Approved = "已通过";
    public const string Rejected = "已驳回";
    public const string Withdrawn = "已撤回";
}

public interface ILeaveService
{
    /// <summary>STU-15 提交离校/返校报备（状态=待批）</summary>
    Task<LeaveApplication> SubmitAsync(LeaveSubmitDto dto);

    /// <summary>COUN-01 辅导员列表（可按状态筛选）</summary>
    Task<PagedResult<LeaveApplication>> GetPagedAsync(int page, int pageSize, string? status = null);

    /// <summary>STU-16 我的报备列表</summary>
    Task<PagedResult<LeaveApplication>> GetByStudentPagedAsync(string studentId, int page, int pageSize);

    /// <summary>COUN-02 审批通过（仅待批）</summary>
    Task<LeaveApplication> ApproveAsync(int applyId);

    /// <summary>COUN-03 驳回（仅待批；原因必填，落 REASON 列）</summary>
    Task<LeaveApplication> RejectAsync(int applyId, string reason);

    /// <summary>STU-17 修改报备（仅待批）</summary>
    Task<LeaveApplication> UpdateAsync(int applyId, LeaveUpdateDto dto);

    /// <summary>STU-40 撤回报备（仅待批 → 已撤回）</summary>
    Task<LeaveApplication> CancelAsync(int applyId);

    /// <summary>COUN-04 离校统计（总数/状态分布/当前离校人数/目的地分布）</summary>
    Task<object> GetStatsAsync();
}
