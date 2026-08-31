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

    /// <summary>
    /// 公告分页列表（对齐契约 GET /notices）：置顶优先，再按发布时间倒序。
    /// 041 起置顶两列并入 D_Notice 本体，直接按 Is_Pinned 排序。
    /// </summary>
    public override async Task<(List<Notice> Items, int Total)> GetPagedAsync(int page, int pageSize)
    {
        var query = _dbSet.AsNoTracking().AsQueryable();

        var total = await query.CountAsync();
        var items = await query
            .OrderBy(n => n.IsPinned == "是" ? 0 : 1)
            .ThenByDescending(n => n.PublishTime)
            .ThenByDescending(n => n.NoticeId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, total);
    }
}
