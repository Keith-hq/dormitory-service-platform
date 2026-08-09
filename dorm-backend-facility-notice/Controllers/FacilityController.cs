using Microsoft.AspNetCore.Mvc;
using DormBackendFacilityNotice.DTO;
using DormBackendFacilityNotice.Services;

namespace DormBackendFacilityNotice.Controllers;

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

    /// <summary>分页查询公共设施（可筛选状态）</summary>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<object>>>> GetPaged(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? status = null)
    {
        var result = await _service.GetPagedAsync(page, pageSize, status);
        return Ok(ApiResponse.Ok(result));
    }

    /// <summary>新增公共设施（主数据）</summary>
    [HttpPost]
    public async Task<ActionResult<ApiResponse<object>>> Create([FromBody] FacilityCreateDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse.Error(400, "参数校验失败"));

        var facility = await _service.CreateAsync(dto);
        return Ok(ApiResponse.Created(facility));
    }
}
