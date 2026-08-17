using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TemplateDormApi.DTO;
using TemplateDormApi.Security;
using TemplateDormApi.Services;

namespace TemplateDormApi.Controllers;

/// <summary>
/// 楼栋管理接口（路由显式复数 /api/buildings，对齐 Apifox 契约）
/// </summary>
[ApiController]
[Route("api/buildings")]
public class BuildingController : ControllerBase
{
    private readonly IBuildingService _service;

    public BuildingController(IBuildingService service)
    {
        _service = service;
    }

    /// <summary>DORM-01 楼栋列表 — 分页查询（宿管端，IT-C1-001 学生 token 应 403）</summary>
    [HttpGet]
    [Authorize(Policy = AuthPolicies.DormAdmin)]
    public async Task<ActionResult<ApiResponse<PagedResult<object>>>> GetPaged(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? buildingType = null)
    {
        if (page < 1 || pageSize < 1 || pageSize > 100)
            return BadRequest(ApiResponse.Error(400, "分页参数不合法：page >= 1，1 <= pageSize <= 100"));

        var result = await _service.GetPagedAsync(page, pageSize, buildingType);
        return Ok(ApiResponse.Ok(result));
    }

    /// <summary>根据 ID 查询楼栋详情（宿管端）</summary>
    [HttpGet("{id}")]
    [Authorize(Policy = AuthPolicies.DormAdmin)]
    public async Task<ActionResult<ApiResponse<object>>> GetById(int id)
    {
        var building = await _service.GetByIdAsync(id);
        if (building == null)
            return NotFound(ApiResponse.Error(404, "楼栋不存在"));

        return Ok(ApiResponse.Ok(building));
    }

    /// <summary>新增楼栋（宿管端，S2 写操作授权；校验失败由 InvalidModelStateResponseFactory 统一返回）</summary>
    [HttpPost]
    [Authorize(Policy = AuthPolicies.DormAdmin)]
    public async Task<ActionResult<ApiResponse<object>>> Create([FromBody] BuildingCreateDto dto)
    {
        var building = await _service.CreateAsync(dto);
        return Ok(ApiResponse.Created(building));
    }

    /// <summary>编辑楼栋信息（宿管端）</summary>
    [HttpPut("{id}")]
    [Authorize(Policy = AuthPolicies.DormAdmin)]
    public async Task<ActionResult<ApiResponse<object>>> Update(int id, [FromBody] BuildingUpdateDto dto)
    {
        var building = await _service.UpdateAsync(id, dto);
        if (building == null)
            return NotFound(ApiResponse.Error(404, "楼栋不存在"));

        return Ok(ApiResponse.Ok(building, "更新成功"));
    }

    /// <summary>删除楼栋（宿管端）</summary>
    [HttpDelete("{id}")]
    [Authorize(Policy = AuthPolicies.DormAdmin)]
    public async Task<ActionResult<ApiResponse<object>>> Delete(int id)
    {
        var success = await _service.DeleteAsync(id);
        if (!success)
            return NotFound(ApiResponse.Error(404, "楼栋不存在"));

        return Ok(ApiResponse.Ok(new { }, "删除成功"));
    }
}
