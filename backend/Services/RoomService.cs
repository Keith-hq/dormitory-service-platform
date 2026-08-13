using System.Collections.Concurrent;
using TemplateDormApi.DTO;
using TemplateDormApi.Exceptions;
using TemplateDormApi.Models;
using TemplateDormApi.Repository;

namespace TemplateDormApi.Services;

public class RoomService : IRoomService
{
    private readonly RoomRepository _repo;
    public RoomService(RoomRepository repo) => _repo = repo;

    /// <summary>
    /// DORM-06 幂等缓存：Idempotency-Key → 已创建房间ID列表。
    /// 注：进程内缓存（DDL 冻结不加幂等落库表），服务重启后失效——挂账，8/14 联调核对口径。
    /// </summary>
    private static readonly ConcurrentDictionary<string, IReadOnlyList<int>> BatchInitCache = new();

    public async Task<PagedResult<Room>> GetPagedAsync(int page, int pageSize, int? buildingId = null)
    {
        var (items, total) = await _repo.GetPagedFilteredAsync(page, pageSize, buildingId);
        return new PagedResult<Room> { Items = items, Total = total, Page = page, PageSize = pageSize };
    }

    public async Task<Room?> GetByIdAsync(int id) => await _repo.GetByIdAsync(id);

    public async Task<Room> CreateAsync(RoomCreateDto dto)
    {
        return await _repo.AddAsync(new Room
        {
            BuildingId = dto.BuildingId,
            RoomNumber = dto.RoomNo,
            Floor = dto.Floor,
            Capacity = dto.Capacity,
            Occupancy = 0,
            Status = "正常",
            PowerStatus = "正常"
        });
    }

    public async Task<Room?> UpdateAsync(int id, RoomUpdateDto dto)
    {
        var room = await _repo.GetByIdAsync(id);
        if (room == null) return null;
        if (dto.RoomNo != null) room.RoomNumber = dto.RoomNo;
        if (dto.Capacity.HasValue) room.Capacity = dto.Capacity.Value;
        if (dto.Status != null) room.Status = dto.Status;
        return await _repo.UpdateAsync(room);
    }

    public async Task<bool> DeleteAsync(int id) => await _repo.DeleteAsync(id);

    /// <summary>DORM-06 批量初始化：floor + startRoomNo + count 生成连续房间号，重复房间跳过</summary>
    public async Task<object> BatchInitAsync(RoomBatchInitDto dto, string? idempotencyKey)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey))
            throw new BusinessException(400, "缺少 Idempotency-Key 请求头（批量初始化要求幂等键）");

        // 幂等：同键重放直接返回首次结果
        if (BatchInitCache.TryGetValue(idempotencyKey, out var cached))
            return new { idempotencyKey, createdRoomIds = cached, total = cached.Count, replayed = true };

        if (!await _repo.BuildingExistsAsync(dto.BuildingId))
            throw new BusinessException(404, "楼栋不存在", 404);

        if (!int.TryParse(dto.StartRoomNo, out var startNo) || startNo <= 0)
            throw new BusinessException(400, "起始房间号必须为正整数（如 101）");

        var existing = await _repo.GetRoomNumbersAsync(dto.BuildingId, dto.Floor);
        var newRooms = new List<Room>();
        var skipped = new List<string>();
        for (var i = 0; i < dto.Count; i++)
        {
            var roomNo = (startNo + i).ToString();
            if (existing.Contains(roomNo))
            {
                skipped.Add(roomNo); // 已存在则跳过，避免主键/重复房间
                continue;
            }
            var room = new Room
            {
                BuildingId = dto.BuildingId,
                RoomNumber = roomNo,
                Floor = dto.Floor,
                Capacity = dto.Capacity,
                Occupancy = 0,
                Status = "正常",
                PowerStatus = "正常"
            };
            _repo.TrackNew(room);
            newRooms.Add(room);
        }

        await _repo.SaveAsync(); // 单次 SaveChanges：批量插入原子提交

        var ids = newRooms.Select(r => r.RoomId).ToList();
        BatchInitCache[idempotencyKey] = ids;
        return new { idempotencyKey, createdRoomIds = ids, total = ids.Count, skipped, replayed = false };
    }
}
