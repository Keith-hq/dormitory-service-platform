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

    /// <summary>DORM-06 批量初始化：楼栋存在性校验</summary>
    public Task<bool> BuildingExistsAsync(int buildingId)
        => _context.Buildings.AnyAsync(b => b.BuildingId == buildingId);

    /// <summary>DORM-06 批量初始化：该楼栋该楼层已有房间号（跳过重复）</summary>
    public async Task<HashSet<string>> GetRoomNumbersAsync(int buildingId, int floor)
        => (await _dbSet
                .Where(r => r.BuildingId == buildingId && r.Floor == floor)
                .Select(r => r.RoomNumber)
                .ToListAsync())
            .ToHashSet();

    /// <summary>DORM-06 批量初始化：批量登记新房间（最后统一 SaveChanges，保持原子）</summary>
    public void TrackNew(Room room) => _context.Rooms.Add(room);

    public Task SaveAsync() => _context.SaveChangesAsync();
}
