using TemplateDormApi.DTO;
using TemplateDormApi.Models;

namespace TemplateDormApi.Services;

/// <summary>
/// 公共设施业务逻辑接口
/// </summary>
public interface IFacilityService
{
    Task<PagedResult<Facility>> GetPagedAsync(int page, int pageSize, int? buildingId = null,
        string? facilityType = null, string? status = null);
    Task<Facility?> GetByIdAsync(int id);
    Task<Facility> CreateAsync(FacilityCreateDto dto);
    Task<Facility?> UpdateAsync(int id, FacilityUpdateDto dto);
    Task<bool> DeleteAsync(int id);
}
