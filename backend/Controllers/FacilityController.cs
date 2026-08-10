using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TemplateDormApi.DTO;
using TemplateDormApi.Models;
using TemplateDormApi.Security;
using TemplateDormApi.Services;

namespace TemplateDormApi.Controllers;

/// <summary>
/// 公共设施管理接口（路由对齐 Apifox 契约 /facilities）
/// </summary>
[ApiController]
[Route("api/facilities")]
public class FacilityController : ControllerBase
{
    private readonly IFacilityService _service;

    public FacilityController(IFacilityService service)
    {
        _service = service;
    }

    /// <summary>分页查询公共设施（可按楼栋/类型/状态筛选，对齐契约 GET /facilities）</summary>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<Facility>>>> GetPaged(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] int? buildingId = null,
        [FromQuery] string? facilityType = null,
        [FromQuery] string? status = null)
    {
        if (page < 1 || pageSize < 1 || pageSize > 100)
            return BadRequest(ApiResponse.Error(400, "分页参数不合法：page >= 1，1 <= pageSize <= 100"));

        var result = await _service.GetPagedAsync(page, pageSize, buildingId, facilityType, status);
        return Ok(ApiResponse.Ok(result));
    }

    /// <summary>根据 ID 查询设施详情</summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<ApiResponse<object>>> GetById(int id)
    {
        var facility = await _service.GetByIdAsync(id);
        if (facility == null)
            return NotFound(ApiResponse.Error(404, "设施不存在"));

        return Ok(ApiResponse.Ok(facility));
    }

    /// <summary>新增公共设施（宿管端主数据，S2 写操作授权）</summary>
    [HttpPost]
    [Authorize(Policy = AuthPolicies.DormAdmin)]
    public async Task<ActionResult<ApiResponse<object>>> Create([FromBody] FacilityCreateDto dto)
    {
        var facility = await _service.CreateAsync(dto);
        return Ok(ApiResponse.Created(facility));
    }

    /// <summary>编辑公共设施（宿管端）</summary>
    [HttpPut("{id}")]
    [Authorize(Policy = AuthPolicies.DormAdmin)]
    public async Task<ActionResult<ApiResponse<object>>> Update(int id, [FromBody] FacilityUpdateDto dto)
    {
        var facility = await _service.UpdateAsync(id, dto);
        if (facility == null)
            return NotFound(ApiResponse.Error(404, "设施不存在"));

        return Ok(ApiResponse.Ok(facility, "更新成功"));
    }

    /// <summary>删除公共设施（宿管端）</summary>
    [HttpDelete("{id}")]
    [Authorize(Policy = AuthPolicies.DormAdmin)]
    public async Task<ActionResult<ApiResponse<object>>> Delete(int id)
    {
        var success = await _service.DeleteAsync(id);
        if (!success)
            return NotFound(ApiResponse.Error(404, "设施不存在"));

        return Ok(ApiResponse.Ok(new { }, "删除成功"));
    }
}
