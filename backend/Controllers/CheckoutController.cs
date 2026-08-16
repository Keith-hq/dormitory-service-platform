using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TemplateDormApi.DTO;
using TemplateDormApi.Services;

namespace TemplateDormApi.Controllers;

/// <summary>
/// 退宿清算 — DORM-11 登记 / DORM-35 状态 / DORM-36 开始清算（三步校验）/ DORM-37 确认 / DORM-38 取消
/// 状态机：待清算 → 已拒绝 / 已通过 / 已取消
/// 鉴权：退宿清算为学生自助流程（IT-C2-001/002 学生 token），任意登录角色可访问。
/// </summary>
[ApiController]
[Authorize]
public class CheckoutController : ControllerBase
{
    private readonly ICheckoutService _service;
    public CheckoutController(ICheckoutService service) => _service = service;

    /// <summary>DORM-11 退宿登记 — 契约 POST /allocations/{allocationId}/checkout-register {reason, checkoutDate}</summary>
    [HttpPost("api/allocations/{allocationId}/checkout-register")]
    public async Task<ActionResult<ApiResponse<object>>> Register(long allocationId, [FromBody] CheckoutRegisterDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ApiResponse.Error(400, "参数校验失败"));
        var log = await _service.RegisterAsync(allocationId, dto);
        return Ok(ApiResponse.Created(log));
    }

    /// <summary>DORM-35 清算状态查询 — 契约 GET /checkouts/{checkoutId}</summary>
    [HttpGet("api/checkouts/{checkoutId}")]
    public async Task<ActionResult<ApiResponse<object>>> Get(int checkoutId)
    {
        var summary = await _service.GetAsync(checkoutId);
        return Ok(ApiResponse.Ok(summary));
    }

    /// <summary>DORM-36 开始清算（三步校验） — 契约 POST /checkouts/{checkoutId}/settle</summary>
    [HttpPost("api/checkouts/{checkoutId}/settle")]
    public async Task<ActionResult<ApiResponse<object>>> Settle(int checkoutId)
    {
        var result = await _service.SettleAsync(checkoutId);
        return Ok(ApiResponse.Ok(result));
    }

    /// <summary>DORM-37 确认退宿（释放床位，幂等） — 契约 POST /checkouts/{checkoutId}/confirm {checkoutDate}</summary>
    [HttpPost("api/checkouts/{checkoutId}/confirm")]
    public async Task<ActionResult<ApiResponse<object>>> Confirm(int checkoutId, [FromBody] CheckoutConfirmDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ApiResponse.Error(400, "参数校验失败"));
        var summary = await _service.ConfirmAsync(checkoutId, dto);
        return Ok(ApiResponse.Ok(summary, "退宿确认完成"));
    }

    /// <summary>DORM-38 取消清算 — 契约 POST /checkouts/{checkoutId}/cancel</summary>
    [HttpPost("api/checkouts/{checkoutId}/cancel")]
    public async Task<ActionResult<ApiResponse<object>>> Cancel(int checkoutId)
    {
        var summary = await _service.CancelAsync(checkoutId);
        return Ok(ApiResponse.Ok(summary, "清算已取消"));
    }
}
