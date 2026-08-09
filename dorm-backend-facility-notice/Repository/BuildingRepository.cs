using Microsoft.EntityFrameworkCore;
using DormBackendFacilityNotice.Data;
using DormBackendFacilityNotice.Models;

namespace DormBackendFacilityNotice.Repository;

/// <summary>
/// 楼栋 Repository
/// </summary>
public class BuildingRepository : BaseRepository<Building>
{
    public BuildingRepository(AppDbContext context) : base(context) { }

    /// <summary>根据楼栋类型筛选分页查询</summary>
    public async Task<(List<Building> Items, int Total)> GetPagedFilteredAsync(
        int page, int pageSize, string? buildingType = null)
    {
        var query = _dbSet.AsQueryable();

        if (!string.IsNullOrWhiteSpace(buildingType))
            query = query.Where(b => b.BuildingType == buildingType);

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(b => b.CreateTime)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, total);
    }
}
