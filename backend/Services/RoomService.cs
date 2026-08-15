using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using TemplateDormApi.DTO;
using TemplateDormApi.Exceptions;
using TemplateDormApi.Models;
using TemplateDormApi.Repository;

namespace TemplateDormApi.Services;

public class RoomService : IRoomService
{
    private const string UkRoomBuildingNo = "UK_D_ROOM_BUILDING_NO";
    private const int MaxUkConflictRetries = 3;

    private readonly RoomRepository _repo;
    public RoomService(RoomRepository repo) => _repo = repo;

    /// <summary>
    /// DORM-06 幂等缓存：Idempotency-Key → 已创建房间ID列表。
    /// 进程内缓存关闭同键并发窗口（同键仅首次执行，重放直接返回）；服务重启后缓存失效。
    /// 跨键并发撞号（两个批次都含同一房间号且都过了预查）由迁移 023 的
    /// UK_D_ROOM_BUILDING_NO (Building_ID, Room_Number) 唯一索引兜底，见 SaveBatchWithUkFallbackAsync。
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

        var existing = await _repo.GetRoomNumbersAsync(dto.BuildingId); // 楼栋级查重（UK 楼栋级唯一，跨楼层同号也跳过）
        var newRooms = new List<Room>();
        var skipped = new List<string>();
        for (var i = 0; i < dto.Count; i++)
        {
            var roomNo = (startNo + i).ToString();
            if (existing.Contains(roomNo))
            {
                skipped.Add(roomNo); // 楼栋内已存在（含跨楼层）则跳过，避免撞 UK_D_ROOM_BUILDING_NO
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

        await SaveBatchWithUkFallbackAsync(dto, newRooms, skipped);

        var ids = newRooms.Select(r => r.RoomId).ToList();
        BatchInitCache[idempotencyKey] = ids;
        return new { idempotencyKey, createdRoomIds = ids, total = ids.Count, skipped, replayed = false };
    }

    /// <summary>
    /// DORM-06 唯一索引兜底：并发批次（不同幂等键、同号房间、均过了预查）插入时数据库报
    /// ORA-00001 命中 UK_D_ROOM_BUILDING_NO。兜底策略：清跟踪 → 楼栋级重查已有房间号
    /// （跨楼层同号同样命中唯一约束）→ 冲突号归入"已存在跳过" → 剩余重试，不 500。
    /// 每轮至少过滤掉一个冲突号（集合单调收敛），重试上限仅在持续并发竞争时快速失败，理论不可达。
    /// </summary>
    private async Task SaveBatchWithUkFallbackAsync(RoomBatchInitDto dto, List<Room> newRooms, List<string> skipped)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await _repo.SaveAsync(); // 单次 SaveChanges：批量插入原子提交
                return;
            }
            catch (DbUpdateException ex)
                when (OracleConstraintParser.TryGetUniqueConstraintName(ex) == UkRoomBuildingNo)
            {
                if (attempt >= MaxUkConflictRetries)
                    throw; // 兜底上限：持续并发竞争时快速失败，避免无限重试

                _repo.ClearTracker();
                var nowExisting = await _repo.GetRoomNumbersAsync(dto.BuildingId); // 楼栋级重查：跨楼层同号同样归入跳过
                for (var i = newRooms.Count - 1; i >= 0; i--)
                {
                    if (nowExisting.Contains(newRooms[i].RoomNumber))
                    {
                        skipped.Add(newRooms[i].RoomNumber); // 并发批次抢先建了同号房间：按已存在跳过
                        newRooms.RemoveAt(i);
                    }
                }
                if (newRooms.Count == 0)
                    return; // 全部被并发批次抢先：按"已存在跳过"语义收尾，不再保存
                foreach (var room in newRooms)
                    _repo.TrackNew(room);
            }
        }
    }
}
