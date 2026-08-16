using Microsoft.EntityFrameworkCore;
using TemplateDormApi.Data;
using TemplateDormApi.Models;

namespace TemplateDormApi.Repository;

/// <summary>
/// 快递记录 Repository（继承 BaseRepository&lt;ParcelRecord&gt; 获得基础 CRUD）。
/// </summary>
public class ParcelRepository : BaseRepository<ParcelRecord>
{
    public ParcelRepository(AppDbContext context) : base(context) { }

    /// <summary>分页查询某学生的快递记录（对齐契约 GET /students/{studentId}/packages）。</summary>
    public async Task<(List<ParcelRecord> Items, int Total)> GetPagedByStudentAsync(
        string studentId, int page, int pageSize)
    {
        var query = _dbSet.AsNoTracking().Where(p => p.StudentId == studentId);
        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(p => p.ParcelId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, total);
    }
}
