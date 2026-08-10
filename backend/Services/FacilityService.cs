using Microsoft.AspNetCore.Http;
using TemplateDormApi.DTO;
using TemplateDormApi.Exceptions;
using TemplateDormApi.Models;
using TemplateDormApi.Repository;

namespace TemplateDormApi.Services;

/// <summary>
/// 公共设施业务逻辑实现
/// </summary>
public class FacilityService : IFacilityService
{
    /// <summary>设施状态白名单（与 D_FACILITY 的 CHECK 约束一致）</summary>
    private static readonly string[] ValidStatuses = { "正常", "维修", "停用" };

    private readonly FacilityRepository _repository;
    private readonly BuildingRepository _buildingRepository;

    public FacilityService(FacilityRepository repository, BuildingRepository buildingRepository)
    {
        _repository = repository;
        _buildingRepository = buildingRepository;
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
            Total = total,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<Facility?> GetByIdAsync(int id)
        => await _repository.GetByIdAsync(id);

    public async Task<Facility> CreateAsync(FacilityCreateDto dto)
    {
        if (!ValidStatuses.Contains(dto.Status))
            throw new ArgumentException($"状态不合法，只能是：{string.Join(" / ", ValidStatuses)}");

        // S6：预检楼栋存在，避免错误 Building_ID 落到 Oracle 外键异常转 500
        if (await _buildingRepository.GetByIdAsync(dto.BuildingId) == null)
            throw new BusinessException(404, "楼栋不存在", StatusCodes.Status404NotFound);

        var facility = new Facility
        {
            BuildingId = dto.BuildingId,
            FacilityCode = dto.FacilityCode,
            FacilityType = dto.FacilityType,
            Status = dto.Status
        };
        return await _repository.AddAsync(facility);
    }

    public async Task<Facility?> UpdateAsync(int id, FacilityUpdateDto dto)
    {
        var facility = await _repository.GetByIdAsync(id);
        if (facility == null) return null;

        if (!string.IsNullOrWhiteSpace(dto.FacilityCode))
            facility.FacilityCode = dto.FacilityCode;
        if (!string.IsNullOrWhiteSpace(dto.FacilityType))
            facility.FacilityType = dto.FacilityType;
        if (!string.IsNullOrWhiteSpace(dto.Status))
        {
            if (!ValidStatuses.Contains(dto.Status))
                throw new ArgumentException($"状态不合法，只能是：{string.Join(" / ", ValidStatuses)}");
            facility.Status = dto.Status;
        }

        return await _repository.UpdateAsync(facility);
    }

    public async Task<bool> DeleteAsync(int id)
        => await _repository.DeleteAsync(id);
}
