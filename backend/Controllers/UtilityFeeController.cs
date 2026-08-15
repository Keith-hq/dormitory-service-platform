using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TemplateDormApi.DTO;
using TemplateDormApi.Security;
using TemplateDormApi.Services;

namespace TemplateDormApi.Controllers;

/// <summary>
/// 水电账单接口（难点②，宿管端）：DORM-19 录账单、DORM-20 修改、DORM-21 发布、
/// DORM-22 分摊、DORM-23 明细、DORM-24 列表、DORM-25 供电状态。
/// DORM-25 放本控制器而非 BillingController（后者是无鉴权测试触发器，保持全开放现状）。
/// </summary>
[ApiController]
[Route("api")]
[Authorize(Policy = AuthPolicies.DormAdmin)]
public class UtilityFeeController : ControllerBase
{
    private static readonly Regex YearMonthRegex = new(@"^\d{4}-(0[1-9]|1[0-2])$", RegexOptions.Compiled);

    private readonly IUtilityFeeService _service;
    private readonly IBillingService _billingService;

    public UtilityFeeController(IUtilityFeeService service, IBillingService billingService)
    {
        _service = service;
        _billingService = billingService;
    }

    /// <summary>DORM-19：录账单（同一房间同一账期唯一）</summary>
    [HttpPost("utility-fees")]
    public async Task<ActionResult<ApiResponse<object>>> CreateBill([FromBody] CreateUtilityFeeRequest req)
    {
        if (!TryValidateYearMonth(req.YearMonth, out var message))
            return BadRequest(ApiResponse.Error(400, message));

        var feeId = await _service.CreateBill(req);
        return Ok(ApiResponse.Ok(new { feeId }, $"账单创建成功：{req.YearMonth}"));
    }

    /// <summary>DORM-20：修改账单（仅未发布可改）</summary>
    [HttpPut("utility-fees/{id:long}")]
    public async Task<ActionResult<ApiResponse<object>>> UpdateBill(long id, [FromBody] UpdateUtilityFeeRequest req)
    {
        await _service.UpdateBill(id, req);
        return Ok(ApiResponse.Ok(new { feeId = id }, "账单修改成功"));
    }

    /// <summary>DORM-21：发布账单（幂等：已发布返回 400）</summary>
    [HttpPost("utility-fees/{id:long}/publish")]
    public async Task<ActionResult<ApiResponse<object>>> PublishBill(long id)
    {
        await _service.PublishBill(id);
        return Ok(ApiResponse.Ok(new { feeId = id }, "账单发布成功"));
    }

    /// <summary>DORM-22：账单分摊（整月维度，重复触发幂等）</summary>
    [HttpPost("utility-fees/{id:long}/allocate")]
    public async Task<ActionResult<ApiResponse<object>>> AllocateBill(long id)
    {
        var result = await _service.AllocateBill(id);
        return Ok(ApiResponse.Ok(result, $"分摊完成：{result.YearMonth}，本账单明细 {result.DetailCount} 条"));
    }

    /// <summary>DORM-23：查询单笔账单的分摊明细</summary>
    [HttpGet("utility-fees/{id:long}/details")]
    public async Task<ActionResult<ApiResponse<object>>> GetBillDetails(long id)
    {
        var result = await _service.GetBillDetails(id);
        return Ok(ApiResponse.Ok(result));
    }

    /// <summary>DORM-24：账单列表（按账期/发布状态过滤）</summary>
    [HttpGet("utility-fees")]
    public async Task<ActionResult<ApiResponse<object>>> GetBills(
        [FromQuery] string? yearMonth = null,
        [FromQuery] string? publishStatus = null)
    {
        if (yearMonth is not null && !TryValidateYearMonth(yearMonth, out var message))
            return BadRequest(ApiResponse.Error(400, message));

        var bills = await _service.GetBills(yearMonth, publishStatus);
        return Ok(ApiResponse.Ok(bills));
    }

    /// <summary>DORM-25：查询房间供电状态</summary>
    [HttpGet("rooms/{roomId:long}/power-status")]
    public async Task<ActionResult<ApiResponse<object>>> GetPowerStatus(long roomId)
    {
        var status = await _billingService.GetPowerStatus((int)roomId);
        return Ok(ApiResponse.Ok(new { roomId, powerStatus = status }));
    }

    /// <summary>校验 yyyy-MM 格式及账期范围（不早于 2020-01，不晚于下月）</summary>
    private static bool TryValidateYearMonth(string yearMonth, out string message)
    {
        if (!YearMonthRegex.IsMatch(yearMonth))
        {
            message = "yearMonth 格式必须为 yyyy-MM";
            return false;
        }

        var now = DateTime.Now;
        var currentMonth = new DateTime(now.Year, now.Month, 1);
        var minMonth = new DateTime(2020, 1, 1);
        var target = new DateTime(int.Parse(yearMonth[..4]), int.Parse(yearMonth[5..7]), 1);

        if (target < minMonth || target > currentMonth.AddMonths(1))
        {
            message = $"yearMonth 必须在 {minMonth:yyyy-MM} 至 {currentMonth.AddMonths(1):yyyy-MM} 之间";
            return false;
        }

        message = string.Empty;
        return true;
    }
}
