using DormBackendFacilityNotice.DTO;
using DormBackendFacilityNotice.Models;

namespace DormBackendFacilityNotice.Services;

/// <summary>
/// 公共设施业务逻辑接口
/// </summary>
public interface IFacilityService
{
    Task<PagedResult<Facility>> GetPagedAsync(int page, int pageSize, string? status = null);
    Task<Facility?> GetByIdAsync(int id);
    Task<Facility> CreateAsync(FacilityCreateDto dto);
}
