using TemplateDormApi.DTO;
using TemplateDormApi.Models;
using TemplateDormApi.Repository;

namespace TemplateDormApi.Services;

/// <summary>
/// 楼栋业务逻辑实现
/// </summary>
public class BuildingService : IBuildingService
{
    private readonly BuildingRepository _repository;

    public BuildingService(BuildingRepository repository)
    {
        _repository = repository;
    }

    public async Task<PagedResult<Building>> GetPagedAsync(int page, int pageSize, string? buildingType = null)
    {
        var (items, total) = await _repository.GetPagedFilteredAsync(page, pageSize, buildingType);
        return new PagedResult<Building>
        {
            Items = items,
            Total = total,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<Building?> GetByIdAsync(int id)
        => await _repository.GetByIdAsync(id);

    public async Task<Building> CreateAsync(BuildingCreateDto dto)
    {
        var building = new Building
        {
            BuildingName = dto.BuildingName,
            BuildingType = dto.BuildingType,
            FloorCount = dto.FloorCount
        };
        return await _repository.AddAsync(building);
    }

    public async Task<Building?> UpdateAsync(int id, BuildingUpdateDto dto)
    {
        var building = await _repository.GetByIdAsync(id);
        if (building == null) return null;

        if (!string.IsNullOrWhiteSpace(dto.BuildingName))
            building.BuildingName = dto.BuildingName;
        if (!string.IsNullOrWhiteSpace(dto.BuildingType))
            building.BuildingType = dto.BuildingType;
        if (dto.FloorCount.HasValue)
            building.FloorCount = dto.FloorCount.Value;

        return await _repository.UpdateAsync(building);
    }

    public async Task<bool> DeleteAsync(int id)
        => await _repository.DeleteAsync(id);
}
