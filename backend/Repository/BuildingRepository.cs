using Microsoft.EntityFrameworkCore;
using TemplateDormApi.Data;
using TemplateDormApi.Models;

namespace TemplateDormApi.Repository;

/// <summary>
/// 楼栋 Repository
/// </summary>
public class BuildingRepository : BaseRepository<Building>
{
    public BuildingRepository(AppDbContext context) : base(context) { }

    /// <summary>
    /// 楼栋下是否存在房间（删除前置检查，IT-C10-002 ② 业务预检）。
    /// Oracle 兼容（评审整改，8411155 同源）：顶层 AnyAsync 被翻译为
    /// CASE WHEN EXISTS(...) THEN True ELSE False，Oracle 21c 无布尔字面量 → ORA-00904；
    /// 用 CountAsync(...) > 0（翻译为 COUNT(*)）。
    /// </summary>
    public async Task<bool> HasRoomsAsync(int buildingId)
        => await _context.Rooms.CountAsync(r => r.BuildingId == buildingId) > 0;

    /// <summary>根据楼栋类型筛选分页查询</summary>
    public async Task<(List<Building> Items, int Total)> GetPagedFilteredAsync(
        int page, int pageSize, string? buildingType = null)
    {
        var query = _dbSet.AsQueryable();

        if (!string.IsNullOrWhiteSpace(buildingType))
            query = query.Where(b => b.BuildingType == buildingType);

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(b => b.BuildingId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, total);
    }
}
