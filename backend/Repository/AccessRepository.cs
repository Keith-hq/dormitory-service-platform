using Microsoft.EntityFrameworkCore;
using TemplateDormApi.Data;
using TemplateDormApi.DTO;

namespace TemplateDormApi.Repository;

public sealed class AccessRepository : FrameworkRepositoryBase
{
    public AccessRepository(AppDbContext context) : base(context) { }

    public Task<PagedResult<AccessLogDto>> GetLogsAsync(
        AccessLogQueryDto query,
        CancellationToken cancellationToken)
        => PendingAsync<PagedResult<AccessLogDto>>(
            "ACCESS-01",
            "门禁分页筛选及离线数据判定待实现",
            cancellationToken);

    /// <summary>
    /// ACCESS-02 楼内实时密度：当前在楼人数 = 每名学生最新一条门禁方向为「进」的计数；
    /// 额定容量 = 楼栋床位总数（D_Bed_Allocation → D_Room.Building_ID）；
    /// Density = 在楼人数 / 容量。可按 BuildingId 过滤单栋。
    /// </summary>
    public async Task<IReadOnlyList<AccessDensityDto>> GetDensityAsync(
        AccessDensityQueryDto query,
        CancellationToken cancellationToken)
    {
        var buildingsQuery = DbContext.Buildings.AsNoTracking().Select(building => building.BuildingId);
        if (query.BuildingId.HasValue)
        {
            buildingsQuery = buildingsQuery.Where(id => id == query.BuildingId.Value);
        }
        var buildingIds = await buildingsQuery.OrderBy(id => id).ToListAsync(cancellationToken);
        if (buildingIds.Count == 0)
        {
            return Array.Empty<AccessDensityDto>();
        }

        // 当前在楼：每学生最新门禁方向为「进」。数据量小，拉全量后内存聚合，
        // 避免"每学生取最新一条"嵌套查询在 Oracle EF 下的翻译问题。
        var logs = await DbContext.AccessLogs.AsNoTracking()
            .Where(log => log.BuildingId.HasValue && log.StudentId != null)
            .ToListAsync(cancellationToken);

        var onlineByBuilding = logs
            .GroupBy(log => log.BuildingId!.Value)
            .ToDictionary(
                group => group.Key,
                group => group
                    .GroupBy(log => log.StudentId!)
                    .Count(studentGroup =>
                        studentGroup.OrderByDescending(log => log.SwipeTime).First().Direction == "进"));

        // 额定容量：楼栋床位总数
        var capacityByBuilding = await (
                from bed in DbContext.BedAllocations.AsNoTracking()
                join room in DbContext.Rooms.AsNoTracking() on bed.RoomId equals room.RoomId
                where room.BuildingId.HasValue
                group room.BuildingId by room.BuildingId!.Value into g
                select new { BuildingId = g.Key, Capacity = g.Count() })
            .ToDictionaryAsync(x => x.BuildingId, x => x.Capacity, cancellationToken);

        var result = new List<AccessDensityDto>(buildingIds.Count);
        foreach (var buildingId in buildingIds)
        {
            onlineByBuilding.TryGetValue(buildingId, out var online);
            capacityByBuilding.TryGetValue(buildingId, out var capacity);
            result.Add(new AccessDensityDto
            {
                BuildingId = buildingId,
                OnlineCount = online,
                Density = capacity > 0 ? Math.Round((decimal)online / capacity, 2) : 0m
            });
        }

        return result;
    }
}
