using Microsoft.EntityFrameworkCore;
using TemplateDormApi.Data;
using TemplateDormApi.Models;

namespace TemplateDormApi.Repository;

/// <summary>
/// 公告 Repository（继承 BaseRepository&lt;T&gt; 零代码获得 CRUD）
/// </summary>
public class NoticeRepository : BaseRepository<Notice>
{
    public NoticeRepository(AppDbContext context) : base(context) { }

    /// <summary>按 ID 查询公告并加载置顶信息（更新置顶状态需要）</summary>
    public override async Task<Notice?> GetByIdAsync(int id)
        => await _dbSet.Include(n => n.Display).FirstOrDefaultAsync(n => n.NoticeId == id);

    /// <summary>
    /// 公告分页列表（对齐契约 GET /notices）：置顶优先，再按发布时间倒序。
    /// 置顶判断依赖 D_Notice_Display（1:1），无置顶记录的公告排在非置顶位。
    /// </summary>
    public override async Task<(List<Notice> Items, int Total)> GetPagedAsync(int page, int pageSize)
    {
        var query = _dbSet.AsNoTracking().AsQueryable();

        var total = await query.CountAsync();
        var items = await query
            .Include(n => n.Display)
            .OrderBy(n => n.Display != null && n.Display.IsPinned == "是" ? 0 : 1)
            .ThenByDescending(n => n.PublishTime)
            .ThenByDescending(n => n.NoticeId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, total);
    }
}
