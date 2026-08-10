using TemplateDormApi.DTO;
using TemplateDormApi.Models;
using TemplateDormApi.Repository;

namespace TemplateDormApi.Services;

public class RoomService : IRoomService
{
    private readonly RoomRepository _repo;
    public RoomService(RoomRepository repo) => _repo = repo;

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
}
