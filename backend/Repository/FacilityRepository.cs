using Microsoft.EntityFrameworkCore;
using TemplateDormApi.Data;
using TemplateDormApi.Models;

namespace TemplateDormApi.Repository;

/// <summary>
/// 公共设施 Repository（继承 BaseRepository&lt;T&gt; 零代码获得 CRUD）
/// </summary>
public class FacilityRepository : BaseRepository<Facility>
{
    public FacilityRepository(AppDbContext context) : base(context) { }

    /// <summary>
    /// 按楼栋/类型/状态筛选分页查询（参数对齐契约 GET /facilities）。
    /// 契约语义为"查询可用设施"：未显式传 status 时默认只看 Status='正常'，
    /// 管理端全量查询请显式传对应状态或另设口径。
    /// </summary>
    public async Task<(List<Facility> Items, int Total)> GetPagedFilteredAsync(
        int page, int pageSize, int? buildingId = null, string? facilityType = null, string? status = null)
    {
        var query = _dbSet.AsQueryable();

        if (buildingId.HasValue)
            query = query.Where(f => f.BuildingId == buildingId.Value);

        if (!string.IsNullOrWhiteSpace(facilityType))
            query = query.Where(f => f.FacilityType == facilityType);

        // S5：不传 status 默认只看"正常"可用设施
        query = string.IsNullOrWhiteSpace(status)
            ? query.Where(f => f.Status == "正常")
            : query.Where(f => f.Status == status);

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(f => f.FacilityId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, total);
    }
}
