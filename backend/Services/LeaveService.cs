using Microsoft.EntityFrameworkCore;
using TemplateDormApi.Data;
using TemplateDormApi.DTO;
using TemplateDormApi.Exceptions;
using TemplateDormApi.Models;
using TemplateDormApi.Repository;

namespace TemplateDormApi.Services;

/// <summary>
/// 离校报备服务（COUN-01~04 / STU-15~17,40）。
/// 状态机：待批 →（审批）已通过/已驳回，（学生）已撤回；修改与撤销仅在待批可用。
/// </summary>
public class LeaveService : ILeaveService
{
    private readonly LeaveRepository _repo;
    private readonly AppDbContext _context;

    public LeaveService(LeaveRepository repo, AppDbContext context)
    {
        _repo = repo;
        _context = context;
    }

    public async Task<LeaveApplication> SubmitAsync(LeaveSubmitDto dto, int? accountId, bool isDormAdmin)
    {
        if (dto.ReturnDate < dto.LeaveDate)
            throw new BusinessException(400, "返校日期不能早于离校日期");

        // 归属校验（评审整改）：学生只能替自己提交，宿管（DormAdmin）可代办
        var (isAdmin, callerStudentId) = await ResolveCallerAsync(accountId, isDormAdmin);
        if (!isAdmin && !string.Equals(dto.StudentId, callerStudentId, StringComparison.Ordinal))
            throw new BusinessException(403, "无权替他人提交离校报备", 403);

        // Oracle 兼容（8411155 同源）：顶层 AnyAsync → ORA-00904，用 CountAsync
        if (await _context.Students.CountAsync(s => s.StudentId == dto.StudentId) == 0)
            throw new BusinessException(404, "学生不存在", 404);

        return await _repo.AddAsync(new LeaveApplication
        {
            // 主键由序列 SEQ_D_LEAVE_APPLICATION_ID + 触发器生成（迁移 023，WHEN NEW IS NULL），不再 MAX+1
            StudentId = dto.StudentId,
            LeaveDate = dto.LeaveDate,
            ReturnDate = dto.ReturnDate,
            Destination = dto.Destination,
            Status = LeaveStatuses.Pending
        });
    }

    public async Task<PagedResult<LeaveApplication>> GetPagedAsync(int page, int pageSize, string? status = null)
    {
        var (items, total) = await _repo.GetPagedFilteredAsync(page, pageSize, status: status);
        return new PagedResult<LeaveApplication> { Items = items, Total = total, Page = page, PageSize = pageSize };
    }

    public async Task<PagedResult<LeaveApplication>> GetByStudentPagedAsync(
        string studentId, int page, int pageSize, int? accountId, bool isDormAdmin)
    {
        var (isAdmin, callerStudentId) = await ResolveCallerAsync(accountId, isDormAdmin);
        if (!isAdmin && !string.Equals(studentId, callerStudentId, StringComparison.Ordinal))
            throw new BusinessException(403, "无权查看他人的报备", 403);

        var (items, total) = await _repo.GetPagedFilteredAsync(page, pageSize, studentId: studentId);
        return new PagedResult<LeaveApplication> { Items = items, Total = total, Page = page, PageSize = pageSize };
    }

    public async Task<LeaveApplication> ApproveAsync(int applyId)
    {
        var app = await RequirePendingAsync(applyId);
        app.Status = LeaveStatuses.Approved;
        return await _repo.UpdateAsync(app);
    }

    public async Task<LeaveApplication> RejectAsync(int applyId, string reason)
    {
        var app = await RequirePendingAsync(applyId);
        app.Status = LeaveStatuses.Rejected;
        app.Reason = reason; // 驳回必填原因（IT-C2-007 ③），落 REASON 列（迁移 017）
        return await _repo.UpdateAsync(app);
    }

    public async Task<LeaveApplication> UpdateAsync(int applyId, LeaveUpdateDto dto, int? accountId, bool isDormAdmin)
    {
        var app = await RequirePendingAsync(applyId);
        var (isAdmin, callerStudentId) = await ResolveCallerAsync(accountId, isDormAdmin);
        EnsureOwner(isAdmin, app.StudentId, callerStudentId);

        var newLeave = dto.LeaveDate ?? app.LeaveDate;
        var newReturn = dto.ReturnDate ?? app.ReturnDate;
        if (newReturn < newLeave)
            throw new BusinessException(400, "返校日期不能早于离校日期");

        if (dto.LeaveDate.HasValue) app.LeaveDate = dto.LeaveDate.Value;
        if (dto.ReturnDate.HasValue) app.ReturnDate = dto.ReturnDate.Value;
        if (dto.Destination != null) app.Destination = dto.Destination;

        return await _repo.UpdateAsync(app);
    }

    public async Task<LeaveApplication> CancelAsync(int applyId, int? accountId, bool isDormAdmin)
    {
        var app = await RequirePendingAsync(applyId);
        var (isAdmin, callerStudentId) = await ResolveCallerAsync(accountId, isDormAdmin);
        EnsureOwner(isAdmin, app.StudentId, callerStudentId);

        app.Status = LeaveStatuses.Withdrawn;
        return await _repo.UpdateAsync(app);
    }

    public async Task<object> GetStatsAsync()
    {
        var total = await _context.LeaveApplications.CountAsync();

        var byStatus = await _context.LeaveApplications
            .GroupBy(a => a.Status)
            .Select(g => new { status = g.Key, count = g.Count() })
            .OrderByDescending(g => g.count)
            .ToListAsync();

        var today = DateTime.Today;
        var currentAway = await _context.LeaveApplications.CountAsync(a =>
            a.Status == LeaveStatuses.Approved && a.LeaveDate <= today && a.ReturnDate >= today);

        var destinations = await _context.LeaveApplications
            .GroupBy(a => a.Destination)
            .Select(g => new { destination = g.Key, count = g.Count() })
            .OrderByDescending(g => g.count)
            .ThenBy(g => g.destination)
            .Take(10)
            .ToListAsync();

        return new { total, currentAway, byStatus, destinations };
    }

    /// <summary>取报备并强制状态机：非「待批」一律拒绝（IT-C2-007 ② 的"仅待批可用"）</summary>
    private async Task<LeaveApplication> RequirePendingAsync(int applyId)
    {
        var app = await _repo.GetByIdAsync(applyId)
            ?? throw new BusinessException(404, "报备不存在", 404);
        if (app.Status != LeaveStatuses.Pending)
            throw new BusinessException(400, $"仅待批状态的报备可操作（当前：{app.Status}）");
        return app;
    }

    /// <summary>解析调用者身份：DormAdmin 放行；其余按 JWT 账户解析学生身份（与 CheckoutService 同范式）</summary>
    private async Task<(bool IsAdmin, string StudentId)> ResolveCallerAsync(int? accountId, bool isDormAdmin)
    {
        if (isDormAdmin) return (true, string.Empty);

        if (!accountId.HasValue)
            throw new BusinessException(401, "未登录或 Token 无效");

        var studentId = await _context.UserAccounts
            .Where(a => a.AccountId == accountId.Value)
            .Select(a => a.StudentId)
            .FirstOrDefaultAsync();
        if (string.IsNullOrWhiteSpace(studentId))
            throw new BusinessException(401, "当前账户未关联学生身份");

        return (false, studentId);
    }

    /// <summary>归属校验：学生仅能操作本人的报备，宿管放行</summary>
    private static void EnsureOwner(bool isAdmin, string? appStudentId, string callerStudentId)
    {
        if (!isAdmin && !string.Equals(appStudentId, callerStudentId, StringComparison.Ordinal))
            throw new BusinessException(403, "无权操作他人的报备", 403);
    }
}
