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
}
