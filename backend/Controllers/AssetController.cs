using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TemplateDormApi.DTO;
using TemplateDormApi.Security;
using TemplateDormApi.Services;

namespace TemplateDormApi.Controllers;

/// <summary>
/// 资产管理接口（DORM-12~18，路由对齐 Apifox 契约）。
/// 全部为宿管端操作，写操作与查询统一 [Authorize(Policy = DormAdmin)]。
/// </summary>
[ApiController]
[Route("api")]
public sealed class AssetController : ControllerBase
{
    private readonly IAssetService _service;

    public AssetController(IAssetService service)
    {
        _service = service;
    }

    /// <summary>房间资产列表（DORM-12）</summary>
    [HttpGet("rooms/{roomId}/assets")]
    [Authorize(Policy = AuthPolicies.DormAdmin)]
    public async Task<ActionResult<ApiResponse<List<AssetDto>>>> GetByRoom(
        int roomId, CancellationToken cancellationToken = default)
    {
        var data = await _service.GetByRoomAsync(roomId, cancellationToken);
        return Ok(ApiResponse.Ok(data));
    }

    /// <summary>登记资产（DORM-13）</summary>
    [HttpPost("assets")]
    [Authorize(Policy = AuthPolicies.DormAdmin)]
    public async Task<ActionResult<ApiResponse<AssetDto>>> Create(
        [FromBody] CreateAssetRequest request, CancellationToken cancellationToken = default)
    {
        var data = await _service.CreateAsync(request, cancellationToken);
        return Ok(ApiResponse.Ok(data, "登记成功"));
    }

    /// <summary>修改资产/状态（DORM-14-update）</summary>
    [HttpPut("assets/{assetId}")]
    [Authorize(Policy = AuthPolicies.DormAdmin)]
    public async Task<ActionResult<ApiResponse<AssetDto>>> Update(
        int assetId, [FromBody] UpdateAssetRequest request, CancellationToken cancellationToken = default)
    {
        var data = await _service.UpdateAsync(assetId, request, cancellationToken);
        return Ok(ApiResponse.Ok(data, "修改成功"));
    }

    /// <summary>删除资产（关联报修禁止，DORM-14-delete）</summary>
    [HttpDelete("assets/{assetId}")]
    [Authorize(Policy = AuthPolicies.DormAdmin)]
    public async Task<ActionResult<ApiResponse<object>>> Delete(
        int assetId, CancellationToken cancellationToken = default)
    {
        await _service.DeleteAsync(assetId, cancellationToken);
        return Ok(ApiResponse.Ok(new { }, "删除成功"));
    }

    /// <summary>资产盘点（DORM-15）</summary>
    [HttpPost("assets/{assetId}/stocktake")]
    [Authorize(Policy = AuthPolicies.DormAdmin)]
    public async Task<ActionResult<ApiResponse<AssetDto>>> Stocktake(
        int assetId, [FromBody] StocktakeAssetRequest request, CancellationToken cancellationToken = default)
    {
        var data = await _service.StocktakeAsync(assetId, request, cancellationToken);
        return Ok(ApiResponse.Ok(data, "盘点成功"));
    }

    /// <summary>损坏资产转报修（DORM-16）</summary>
    [HttpPost("assets/{assetId}/to-repair")]
    [Authorize(Policy = AuthPolicies.DormAdmin)]
    public async Task<ActionResult<ApiResponse<AssetWarningDto>>> ToRepair(
        int assetId, [FromBody] ToRepairRequest request, CancellationToken cancellationToken = default)
    {
        var data = await _service.ToRepairAsync(assetId, request, cancellationToken);
        return Ok(ApiResponse.Ok(data, "已生成报修工单"));
    }

    /// <summary>损耗预警列表（DORM-17，分页）</summary>
    [HttpGet("assets/warnings")]
    [Authorize(Policy = AuthPolicies.DormAdmin)]
    public async Task<ActionResult<ApiResponse<PagedResult<AssetWarningDto>>>> GetWarnings(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 10, CancellationToken cancellationToken = default)
    {
        if (page < 1 || pageSize < 1 || pageSize > 100)
            return BadRequest(ApiResponse.Error(400, "分页参数不合法：page >= 1，1 <= pageSize <= 100"));

        var data = await _service.GetWarningsAsync(page, pageSize, cancellationToken);
        return Ok(ApiResponse.Ok(data));
    }

    /// <summary>处理损耗预警（DORM-18）</summary>
    [HttpPut("assets/warnings/{assetId}/handle")]
    [Authorize(Policy = AuthPolicies.DormAdmin)]
    public async Task<ActionResult<ApiResponse<AssetWarningDto>>> HandleWarning(
        int assetId, [FromBody] HandleWarningRequest request, CancellationToken cancellationToken = default)
    {
        var data = await _service.HandleWarningAsync(assetId, request, cancellationToken);
        return Ok(ApiResponse.Ok(data, "已处理"));
    }
}
