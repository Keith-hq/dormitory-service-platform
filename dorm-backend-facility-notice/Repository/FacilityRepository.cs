using Microsoft.EntityFrameworkCore;
using DormBackendFacilityNotice.Data;
using DormBackendFacilityNotice.Models;

namespace DormBackendFacilityNotice.Repository;

/// <summary>
/// 公共设施 Repository（继承 BaseRepository&lt;T&gt; 零代码获得 CRUD）
/// </summary>
public class FacilityRepository : BaseRepository<Facility>
{
    public FacilityRepository(AppDbContext context) : base(context) { }

    /// <summary>按楼栋/类型/状态筛选分页查询（参数对齐契约 GET /facilities）</summary>
    public async Task<(List<Facility> Items, int Total)> GetPagedFilteredAsync(
        int page, int pageSize, int? buildingId = null, string? facilityType = null, string? status = null)
    {
        var query = _dbSet.AsQueryable();

        if (buildingId.HasValue)
            query = query.Where(f => f.BuildingId == buildingId.Value);

        if (!string.IsNullOrWhiteSpace(facilityType))
            query = query.Where(f => f.FacilityType == facilityType);

        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(f => f.Status == status);

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(f => f.FacilityId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, total);
    }
}
