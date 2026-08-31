using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TemplateDormApi.DTO;
using TemplateDormApi.Security;
using TemplateDormApi.Services;

namespace TemplateDormApi.Controllers;

/// <summary>
/// 退宿清算 — DORM-11 登记 / DORM-35 状态 / DORM-36 开始清算（两步校验）/ DORM-37 确认 / DORM-38 取消
/// 状态机：待清算 → 已拒绝 / 已通过 / 已取消
/// 鉴权：学生自助流程（IT-C2-001/002 学生 token），服务层按登录态做归属校验（非本人 403）；
/// 宿管（admin/super_admin）显式放行可代办。
/// </summary>
[ApiController]
[Authorize]
public class CheckoutController : ControllerBase
{
    private readonly ICheckoutService _service;
    public CheckoutController(ICheckoutService service) => _service = service;

    /// <summary>解析调用者：DormAdmin 角色放行；否则取账户 ID 供服务层解析学生身份</summary>
    private (int? AccountId, bool IsDormAdmin) ResolveCaller() => (
        CurrentUser.GetAccountId(User),
        User.IsInRole(AuthPolicies.Admin) || User.IsInRole(AuthPolicies.SuperAdmin));

    /// <summary>DORM-11 退宿登记 — 契约 POST /allocations/{allocationId}/checkout-register {reason, checkoutDate}</summary>
    [HttpPost("api/allocations/{allocationId}/checkout-register")]
    public async Task<ActionResult<ApiResponse<object>>> Register(long allocationId, [FromBody] CheckoutRegisterDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ApiResponse.Error(400, "参数校验失败"));
        var caller = ResolveCaller();
        var log = await _service.RegisterAsync(allocationId, dto, caller.AccountId, caller.IsDormAdmin);
        return Ok(ApiResponse.Created(log));
    }

    /// <summary>DORM-35 清算状态查询 — 契约 GET /checkouts/{checkoutId}</summary>
    [HttpGet("api/checkouts/{checkoutId}")]
    public async Task<ActionResult<ApiResponse<object>>> Get(int checkoutId)
    {
        var caller = ResolveCaller();
        var summary = await _service.GetAsync(checkoutId, caller.AccountId, caller.IsDormAdmin);
        return Ok(ApiResponse.Ok(summary));
    }

    /// <summary>DORM-36 开始清算（两步校验） — 契约 POST /checkouts/{checkoutId}/settle</summary>
    [HttpPost("api/checkouts/{checkoutId}/settle")]
    public async Task<ActionResult<ApiResponse<object>>> Settle(int checkoutId)
    {
        var caller = ResolveCaller();
        var result = await _service.SettleAsync(checkoutId, caller.AccountId, caller.IsDormAdmin);
        return Ok(ApiResponse.Ok(result));
    }

    /// <summary>DORM-37 确认退宿（释放床位，幂等） — 契约 POST /checkouts/{checkoutId}/confirm {checkoutDate}</summary>
    [HttpPost("api/checkouts/{checkoutId}/confirm")]
    public async Task<ActionResult<ApiResponse<object>>> Confirm(int checkoutId, [FromBody] CheckoutConfirmDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ApiResponse.Error(400, "参数校验失败"));
        var caller = ResolveCaller();
        var summary = await _service.ConfirmAsync(checkoutId, dto, caller.AccountId, caller.IsDormAdmin);
        return Ok(ApiResponse.Ok(summary, "退宿确认完成"));
    }

    /// <summary>DORM-38 取消清算 — 契约 POST /checkouts/{checkoutId}/cancel</summary>
    [HttpPost("api/checkouts/{checkoutId}/cancel")]
    public async Task<ActionResult<ApiResponse<object>>> Cancel(int checkoutId)
    {
        var caller = ResolveCaller();
        var summary = await _service.CancelAsync(checkoutId, caller.AccountId, caller.IsDormAdmin);
        return Ok(ApiResponse.Ok(summary, "清算已取消"));
    }
}
