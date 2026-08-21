using TemplateDormApi.DTO;
using TemplateDormApi.Models;

namespace TemplateDormApi.Services;

public interface IRoomService
{
    Task<PagedResult<Room>> GetPagedAsync(int page, int pageSize, int? buildingId = null);
    Task<Room?> GetByIdAsync(int id);
    Task<Room> CreateAsync(RoomCreateDto dto);
    Task<Room?> UpdateAsync(int id, RoomUpdateDto dto);
    Task<bool> DeleteAsync(int id);

    /// <summary>DORM-06 批量初始化（Idempotency-Key 幂等）：按楼层/起始房间号/数量生成房间</summary>
    Task<object> BatchInitAsync(RoomBatchInitDto dto, string? idempotencyKey);
}
