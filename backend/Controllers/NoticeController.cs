using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TemplateDormApi.DTO;
using TemplateDormApi.Models;
using TemplateDormApi.Security;
using TemplateDormApi.Services;

namespace TemplateDormApi.Controllers;

/// <summary>
/// 公告管理接口（路由显式用复数 /api/notices，对齐 Apifox 契约）
/// </summary>
[ApiController]
[Route("api/notices")]
public class NoticeController : ControllerBase
{
    private readonly INoticeService _service;

    public NoticeController(INoticeService service)
    {
        _service = service;
    }

    /// <summary>分页查询公告列表（置顶优先、发布时间倒序，对齐契约 GET /notices）</summary>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<NoticeItemDto>>>> GetPaged(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        if (page < 1 || pageSize < 1 || pageSize > 100)
            return BadRequest(ApiResponse.Error(400, "分页参数不合法：page >= 1，1 <= pageSize <= 100"));

        var result = await _service.GetPagedAsync(page, pageSize);
        return Ok(ApiResponse.Ok(result));
    }

    /// <summary>根据 ID 查询公告详情</summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<ApiResponse<object>>> GetById(int id)
    {
        var notice = await _service.GetByIdAsync(id);
        if (notice == null)
            return NotFound(ApiResponse.Error(404, "公告不存在"));

        return Ok(ApiResponse.Ok(notice));
    }

    /// <summary>发布公告（宿管端，S2 写操作授权）</summary>
    [HttpPost]
    [Authorize(Policy = AuthPolicies.DormAdmin)]
    public async Task<ActionResult<ApiResponse<object>>> Create([FromBody] NoticeCreateDto dto)
    {
        var notice = await _service.CreateAsync(dto);
        return Ok(ApiResponse.Created(notice));
    }

    /// <summary>编辑公告（宿管端）</summary>
    [HttpPut("{id}")]
    [Authorize(Policy = AuthPolicies.DormAdmin)]
    public async Task<ActionResult<ApiResponse<object>>> Update(int id, [FromBody] NoticeUpdateDto dto)
    {
        var notice = await _service.UpdateAsync(id, dto);
        if (notice == null)
            return NotFound(ApiResponse.Error(404, "公告不存在"));

        return Ok(ApiResponse.Ok(notice, "更新成功"));
    }

    /// <summary>删除公告（宿管端）</summary>
    [HttpDelete("{id}")]
    [Authorize(Policy = AuthPolicies.DormAdmin)]
    public async Task<ActionResult<ApiResponse<object>>> Delete(int id)
    {
        var success = await _service.DeleteAsync(id);
        if (!success)
            return NotFound(ApiResponse.Error(404, "公告不存在"));

        return Ok(ApiResponse.Ok(new { }, "删除成功"));
    }
}
