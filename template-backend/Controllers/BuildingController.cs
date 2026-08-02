using Microsoft.AspNetCore.Mvc;
using TemplateDormApi.DTO;
using TemplateDormApi.Services;

namespace TemplateDormApi.Controllers;

/// <summary>
/// 楼栋管理接口（标准 Restful API 模板）
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class BuildingController : ControllerBase
{
    private readonly IBuildingService _service;

    public BuildingController(IBuildingService service)
    {
        _service = service;
    }

    /// <summary>分页查询楼栋列表</summary>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<object>>>> GetPaged(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? buildingType = null)
    {
        var result = await _service.GetPagedAsync(page, pageSize, buildingType);
        return Ok(ApiResponse.Ok(result));
    }

    /// <summary>根据 ID 查询楼栋详情</summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<ApiResponse<object>>> GetById(int id)
    {
        var building = await _service.GetByIdAsync(id);
        if (building == null)
            return NotFound(ApiResponse.Error(404, "楼栋不存在"));

        return Ok(ApiResponse.Ok(building));
    }

    /// <summary>新增楼栋</summary>
    [HttpPost]
    public async Task<ActionResult<ApiResponse<object>>> Create([FromBody] BuildingCreateDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse.Error(400, "参数校验失败"));

        var building = await _service.CreateAsync(dto);
        return Ok(ApiResponse.Created(building));
    }

    /// <summary>编辑楼栋信息</summary>
    [HttpPut("{id}")]
    public async Task<ActionResult<ApiResponse<object>>> Update(int id, [FromBody] BuildingUpdateDto dto)
    {
        var building = await _service.UpdateAsync(id, dto);
        if (building == null)
            return NotFound(ApiResponse.Error(404, "楼栋不存在"));

        return Ok(ApiResponse.Ok(building, "更新成功"));
    }

    /// <summary>删除楼栋</summary>
    [HttpDelete("{id}")]
    public async Task<ActionResult<ApiResponse<object>>> Delete(int id)
    {
        var success = await _service.DeleteAsync(id);
        if (!success)
            return NotFound(ApiResponse.Error(404, "楼栋不存在"));

        return Ok(ApiResponse.Ok(new { }, "删除成功"));
    }
}
