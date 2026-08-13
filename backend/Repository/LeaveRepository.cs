using Microsoft.EntityFrameworkCore;
using TemplateDormApi.Data;
using TemplateDormApi.Models;

namespace TemplateDormApi.Repository;

public class LeaveRepository : BaseRepository<LeaveApplication>
{
    public LeaveRepository(AppDbContext context) : base(context) { }

    /// <summary>主键生成：MAX+1（表无序列，DDL 冻结不改；并发撞号由 DbSaveRetry 重试兜底）</summary>
    public async Task<long> NextApplyIdAsync()
        => await _dbSet.AnyAsync() ? await _dbSet.MaxAsync(a => a.ApplyId) + 1L : 1L;

    /// <summary>分页查询：按学生过滤（STU-16）/ 按状态过滤（COUN-01），按报备ID倒序</summary>
    public async Task<(List<LeaveApplication> Items, int Total)> GetPagedFilteredAsync(
        int page, int pageSize, string? studentId = null, string? status = null)
    {
        var query = _dbSet.AsQueryable();
        if (!string.IsNullOrWhiteSpace(studentId))
            query = query.Where(a => a.StudentId == studentId);
        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(a => a.Status == status);

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(a => a.ApplyId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
        return (items, total);
    }
}
