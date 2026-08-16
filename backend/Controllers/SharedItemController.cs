using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TemplateDormApi.DTO;
using TemplateDormApi.Security;
using TemplateDormApi.Services;

namespace TemplateDormApi.Controllers;

/// <summary>
/// 共享物品主数据接口（DORM-48/49，宿管端）。
/// 注意：GET /shared-items（STU-24 学生端可借列表）在 InventoryTxnController，
/// 本控制器仅承载主数据写操作（POST/PUT/DELETE），HTTP 方法不同可共存。
/// </summary>
[ApiController]
[Route("api")]
public sealed class SharedItemController : ControllerBase
{
    private readonly ISharedItemService _service;

    public SharedItemController(ISharedItemService service)
    {
        _service = service;
    }

    /// <summary>发布共享物品（DORM-48）</summary>
    [HttpPost("shared-items")]
    [Authorize(Policy = AuthPolicies.DormAdmin)]
    public async Task<ActionResult<ApiResponse<SharedItemDto>>> Create(
        [FromBody] CreateSharedItemRequest request, CancellationToken cancellationToken = default)
    {
        var data = await _service.CreateAsync(request, cancellationToken);
        return Ok(ApiResponse.Ok(data, "发布成功"));
    }

    /// <summary>维护共享物品（DORM-49-update）</summary>
    [HttpPut("shared-items/{itemId}")]
    [Authorize(Policy = AuthPolicies.DormAdmin)]
    public async Task<ActionResult<ApiResponse<SharedItemDto>>> Update(
        int itemId, [FromBody] UpdateSharedItemRequest request, CancellationToken cancellationToken = default)
    {
        var data = await _service.UpdateAsync(itemId, request, cancellationToken);
        return Ok(ApiResponse.Ok(data, "维护成功"));
    }

    /// <summary>删除共享物品（有借出记录禁止，DORM-49-delete）</summary>
    [HttpDelete("shared-items/{itemId}")]
    [Authorize(Policy = AuthPolicies.DormAdmin)]
    public async Task<ActionResult<ApiResponse<object>>> Delete(
        int itemId, CancellationToken cancellationToken = default)
    {
        await _service.DeleteAsync(itemId, cancellationToken);
        return Ok(ApiResponse.Ok(new { }, "删除成功"));
    }
}
