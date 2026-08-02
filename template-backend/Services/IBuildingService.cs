using TemplateDormApi.DTO;
using TemplateDormApi.Models;

namespace TemplateDormApi.Services;

/// <summary>
/// 楼栋业务逻辑接口
/// </summary>
public interface IBuildingService
{
    Task<PagedResult<Building>> GetPagedAsync(int page, int pageSize, string? buildingType = null);
    Task<Building?> GetByIdAsync(int id);
    Task<Building> CreateAsync(BuildingCreateDto dto);
    Task<Building?> UpdateAsync(int id, BuildingUpdateDto dto);
    Task<bool> DeleteAsync(int id);
}
