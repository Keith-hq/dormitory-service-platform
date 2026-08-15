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

    /// <summary>
    /// DORM-06 批量初始化：该楼栋已有房间号（跳过重复）。
    /// 楼栋级查重，对齐迁移 023 唯一约束 UK_D_ROOM_BUILDING_NO (Building_ID, Room_Number)：
    /// 跨楼层同号同样视为已存在（三审整改）。
    /// </summary>
    public async Task<HashSet<string>> GetRoomNumbersAsync(int buildingId)
        => (await _dbSet
                .Where(r => r.BuildingId == buildingId)
                .Select(r => r.RoomNumber)
                .ToListAsync())
            .ToHashSet();

    /// <summary>DORM-06 批量初始化：批量登记新房间（最后统一 SaveChanges，保持原子）</summary>
    public void TrackNew(Room room) => _context.Rooms.Add(room);

    /// <summary>DORM-06 批量初始化：UK 冲突兜底时清空跟踪（冲突房间按跳过语义重挂）</summary>
    public void ClearTracker() => _context.ChangeTracker.Clear();

    public Task SaveAsync() => _context.SaveChangesAsync();
}
