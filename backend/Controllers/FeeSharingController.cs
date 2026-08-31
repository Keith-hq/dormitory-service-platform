using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TemplateDormApi.Data;
using TemplateDormApi.DTO;
using TemplateDormApi.Security;
using TemplateDormApi.Services;

namespace TemplateDormApi.Controllers;

/// <summary>
/// 水电分摊接口
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class FeeSharingController : ControllerBase
{
    private static readonly Regex YearMonthRegex = new(@"^\d{4}-(0[1-9]|1[0-2])$", RegexOptions.Compiled);

    private readonly IFeeSharingService _service;
    private readonly AppDbContext _context;

    public FeeSharingController(IFeeSharingService service, AppDbContext context)
    {
        _service = service;
        _context = context;
    }

    /// <summary>
    /// 触发月度分摊（宿管端）。
    /// B6：写操作直接执行存储过程，必须宿管角色授权；yearMonth 缺省为当前月份，
    /// 并校验格式与范围，避免误触发错误账期。
    /// </summary>
    [HttpPost("calc-monthly")]
    [Authorize(Policy = AuthPolicies.DormAdmin)]
    public async Task<IActionResult> CalcMonthlyFee([FromQuery] string? yearMonth = null)
    {
        var targetMonth = string.IsNullOrWhiteSpace(yearMonth)
            ? DateTime.Now.ToString("yyyy-MM")
            : yearMonth;

        if (!TryValidateYearMonth(targetMonth, out var message))
            return BadRequest(ApiResponse.Error(400, message));

        await _service.CalcMonthlyFee(targetMonth);
        return Ok(ApiResponse.Ok(new { yearMonth = targetMonth }, $"月度分摊完成：{targetMonth}"));
    }

    /// <summary>触发退宿结算（宿管端，B6 授权 + 参数校验）</summary>
    [HttpPost("calc-checkout")]
    [Authorize(Policy = AuthPolicies.DormAdmin)]
    public async Task<IActionResult> CalcCheckoutFee(
        [FromQuery] string studentId,
        [FromQuery] int allocationId)
    {
        if (string.IsNullOrWhiteSpace(studentId))
            return BadRequest(ApiResponse.Error(400, "studentId 不能为空"));
        if (allocationId < 1)
            return BadRequest(ApiResponse.Error(400, "allocationId 必须大于等于 1"));

        // 一审 R1：事务从服务方法移到接口入口——服务方法保持无事务，
        // 供退宿流程（PR #49）在其自身外层事务内直接调用同一方法。
        // 本接口作为独立入口，在此显式提交（SP 内部不 COMMIT）。
        await using var tx = await _context.Database.BeginTransactionAsync();
        await _service.CalcCheckoutFee(studentId, allocationId);
        await tx.CommitAsync();

        return Ok(ApiResponse.Ok(new { studentId, allocationId }, $"退宿分摊完成：Student={studentId}, Allocation={allocationId}"));
    }

    /// <summary>查询个人分摊明细</summary>
    [HttpGet("detail")]
    public async Task<IActionResult> GetFeeDetail(
        [FromQuery] string studentId,
        [FromQuery] string yearMonth)
    {
        if (string.IsNullOrWhiteSpace(studentId))
            return BadRequest(ApiResponse.Error(400, "studentId 不能为空"));
        if (!TryValidateYearMonth(yearMonth, out var message))
            return BadRequest(ApiResponse.Error(400, message));

        var result = await _service.GetFeeDetail(studentId, yearMonth);
        return Ok(new
        {
            studentId,
            yearMonth,
            count = result.Count,
            items = result.Select(f => new
            {
                f.DetailId,
                waterShare = f.WaterShare,
                powerShare = f.PowerShare,
                total = f.WaterShare + f.PowerShare,
                f.StayDays,
                f.BillType,
                f.IsPaid
            })
        });
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
