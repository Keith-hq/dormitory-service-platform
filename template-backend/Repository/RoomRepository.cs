using Microsoft.EntityFrameworkCore;
using TemplateDormApi.Data;
using TemplateDormApi.Models;

namespace TemplateDormApi.Repository;

public class RoomRepository : BaseRepository<Room>
{
    public RoomRepository(AppDbContext context) : base(context) { }

    public async Task<(List<Room> Items, int Total)> GetPagedFilteredAsync(
        int page, int pageSize, int? buildingId = null)
    {
        var query = _dbSet.AsQueryable();
        if (buildingId.HasValue)
            query = query.Where(r => r.BuildingId == buildingId.Value);

        var total = await query.CountAsync();
        var items = await query
            .OrderBy(r => r.BuildingId).ThenBy(r => r.RoomNumber)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync();
        return (items, total);
    }
}
