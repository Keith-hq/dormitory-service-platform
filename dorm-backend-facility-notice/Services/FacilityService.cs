using DormBackendFacilityNotice.DTO;
using DormBackendFacilityNotice.Models;
using DormBackendFacilityNotice.Repository;

namespace DormBackendFacilityNotice.Services;

/// <summary>
/// 公共设施业务逻辑实现
/// </summary>
public class FacilityService : IFacilityService
{
    /// <summary>设施状态白名单（与 D_FACILITY 的 CHECK 约束一致）</summary>
    private static readonly string[] ValidStatuses = { "正常", "维修", "停用" };

    private readonly FacilityRepository _repository;

    public FacilityService(FacilityRepository repository)
    {
        _repository = repository;
    }

    public async Task<PagedResult<Facility>> GetPagedAsync(
        int page, int pageSize, int? buildingId = null, string? facilityType = null, string? status = null)
    {
        // 状态不合法直接抛业务异常 → ExceptionMiddleware 统一返回 400
        if (!string.IsNullOrWhiteSpace(status) && !ValidStatuses.Contains(status))
            throw new ArgumentException($"状态不合法，只能是：{string.Join(" / ", ValidStatuses)}");

        var (items, total) = await _repository.GetPagedFilteredAsync(page, pageSize, buildingId, facilityType, status);
        return new PagedResult<Facility>
        {
            Items = items,
            Total = total
        };
    }

    public async Task<Facility?> GetByIdAsync(int id)
        => await _repository.GetByIdAsync(id);

    public async Task<Facility> CreateAsync(FacilityCreateDto dto)
    {
        if (!ValidStatuses.Contains(dto.Status))
            throw new ArgumentException($"状态不合法，只能是：{string.Join(" / ", ValidStatuses)}");

        var facility = new Facility
        {
            BuildingId = dto.BuildingId,
            FacilityCode = dto.FacilityCode,
            FacilityType = dto.FacilityType,
            Status = dto.Status
        };
        return await _repository.AddAsync(facility);
    }
}
