using Microsoft.EntityFrameworkCore;
using TemplateDormApi.Data;
using TemplateDormApi.Models;

namespace TemplateDormApi.Repository;

/// <summary>
/// 访客授权 Repository（继承 BaseRepository&lt;VisitorAuthorization&gt; 获得基础 CRUD）。
/// </summary>
public class VisitorRepository : BaseRepository<VisitorAuthorization>
{
    public VisitorRepository(AppDbContext context) : base(context) { }

    /// <summary>分页查询某学生的访客授权记录（对齐契约 GET /students/{studentId}/visitor-authorizations）。</summary>
    public async Task<(List<VisitorAuthorization> Items, int Total)> GetPagedByStudentAsync(
        string studentId, int page, int pageSize)
    {
        var query = _dbSet.AsNoTracking().Where(v => v.StudentId == studentId);
        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(v => v.AuthorizationId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, total);
    }

    /// <summary>只读查询当前学生在住房间（D_Bed_Allocation 中 CheckOut_Date 为空）。</summary>
    public async Task<long?> GetActiveRoomIdAsync(string studentId)
        => await _context.Set<BedAllocation>()
            .AsNoTracking()
            .Where(b => b.StudentId == studentId && b.CheckOutDate == null)
            .Select(b => b.RoomId)
            .FirstOrDefaultAsync();
}
