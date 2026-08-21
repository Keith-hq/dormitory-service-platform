using Microsoft.EntityFrameworkCore;
using TemplateDormApi.DTO;
using TemplateDormApi.Exceptions;
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
    {
        var building = await _repository.GetByIdAsync(id);
        if (building == null) return false;

        // IT-C10-002 ②：删除被引用楼栋应返回明确业务错误而非 500。
        // 先业务预检（房间归属楼栋，占绝大多数场景），数据库外键兜底（资产等其他关联表）。
        if (await _repository.HasRoomsAsync(id))
            throw new BusinessException(400, "该楼栋下存在房间，请先删除或迁移房间");

        try
        {
            return await _repository.DeleteAsync(id);
        }
        catch (DbUpdateException ex) when (OracleConstraintParser.IsForeignKeyViolation(ex))
        {
            // ORA-02292：存在未预检到的外键关联（如 D_Asset）
            throw new BusinessException(400, "该楼栋存在关联数据（资产等），无法删除");
        }
    }
}
