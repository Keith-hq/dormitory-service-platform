using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TemplateDormApi.Data;
using TemplateDormApi.DTO;
using TemplateDormApi.Exceptions;
using TemplateDormApi.Security;
using TemplateDormApi.Services;

namespace TemplateDormApi.Controllers;

/// <summary>
/// 钱包接口（难点②）：STU-05 人工缴费、STU-06 钱包查询、STU-07 充值。
/// 缴费/充值需 Idempotency-Key 请求头（幂等语义见 database/sp/sp_wallet.sql），
/// 学生身份从 JWT 解析（不允许替他人缴费/充值）。
/// </summary>
[ApiController]
[Route("api")]
public class WalletController : ControllerBase
{
    private static readonly Regex YearMonthRegex = new(@"^\d{4}-(0[1-9]|1[0-2])$", RegexOptions.Compiled);

    private readonly IWalletService _service;
    private readonly AppDbContext _context;

    public WalletController(IWalletService service, AppDbContext context)
    {
        _service = service;
        _context = context;
    }

    private const string IdempotencyKeyHeader = "Idempotency-Key";

    /// <summary>从 JWT 解析当前学生的 Student_ID</summary>
    private async Task<string> ResolveStudentId()
    {
        var accountId = CurrentUser.GetAccountId(User)
            ?? throw new BusinessException(401, "未登录或 Token 无效");

        var studentId = await _context.UserAccounts
            .Where(a => a.AccountId == accountId)
            .Select(a => a.StudentId)
            .FirstOrDefaultAsync();

        return studentId ?? throw new BusinessException(401, "当前账户未关联学生身份");
    }

    /// <summary>读取 Idempotency-Key 请求头，为空则返回 null</summary>
    private static string? GetIdempotencyKey(HttpRequest request)
    {
        return request.Headers[IdempotencyKeyHeader].FirstOrDefault();
    }

    /// <summary>
    /// 幂等键 100 字符边界校验：迁移 010 列宽为 VARCHAR2(100 CHAR)，
    /// 超长在入口拒绝，避免 Oracle 500（与 InventoryTxnController 同口径）。
    /// </summary>
    private static bool IsIdempotencyKeyTooLong(string? key)
    {
        return !string.IsNullOrEmpty(key) && key.Length > 100;
    }

    /// <summary>STU-05：人工缴费（需 Idempotency-Key 保证幂等）</summary>
    [HttpPost("wallet/payments")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<object>>> ManualPay([FromBody] ManualPayRequest req)
    {
        var studentId = await ResolveStudentId();
        var idempotencyKey = GetIdempotencyKey(Request);

        if (string.IsNullOrWhiteSpace(idempotencyKey))
            return Ok(ApiResponse.Error(400, "缺少 Idempotency-Key 请求头"));

        if (IsIdempotencyKeyTooLong(idempotencyKey))
            return Ok(ApiResponse.Error(400, "Idempotency-Key 不能超过 100 字符"));

        var rc = await _service.ManualPay(req.DetailId, studentId, idempotencyKey);

        var msgs = new[]
        {
            "缴费成功", "明细不存在", "明细非本人，无法缴费", "钱包余额不足",
            "账单已缴或无需缴费", "Idempotency-Key 已被其它交易占用"
        };
        var msg = rc >= 0 && rc < msgs.Length ? msgs[rc] : "未知错误";
        return rc == 0
            ? Ok(ApiResponse.Ok(new { detailId = req.DetailId }, msg))
            : Ok(ApiResponse.Error(400, msg));
    }

    /// <summary>STU-07：钱包充值（需 Idempotency-Key 保证幂等，无钱包行自动开户）</summary>
    [HttpPost("wallet/recharges")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<object>>> Recharge([FromBody] RechargeRequest req)
    {
        var studentId = await ResolveStudentId();
        var idempotencyKey = GetIdempotencyKey(Request);

        if (string.IsNullOrWhiteSpace(idempotencyKey))
            return Ok(ApiResponse.Error(400, "缺少 Idempotency-Key 请求头"));

        if (IsIdempotencyKeyTooLong(idempotencyKey))
            return Ok(ApiResponse.Error(400, "Idempotency-Key 不能超过 100 字符"));

        var rc = await _service.Recharge(studentId, req.Amount, idempotencyKey);

        var msgs = new[]
        {
            "充值成功", "充值金额必须大于 0", "Idempotency-Key 已被其它交易占用", "学生不存在"
        };
        var msg = rc >= 0 && rc < msgs.Length ? msgs[rc] : "未知错误";
        return rc == 0
            ? Ok(ApiResponse.Ok(new { amount = req.Amount }, msg))
            : Ok(ApiResponse.Error(400, msg));
    }

    /// <summary>STU-06：查询本人钱包（余额 + 指定月份流水，仅本人可见）</summary>
    [HttpGet("students/{studentId}/wallet")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<object>>> GetWallet(
        string studentId,
        [FromQuery] string? yearMonth = null)
    {
        var currentStudentId = await ResolveStudentId();
        if (!string.Equals(studentId, currentStudentId, StringComparison.Ordinal))
            return Ok(ApiResponse.Error(403, "无权查看他人的钱包"));

        var targetMonth = string.IsNullOrWhiteSpace(yearMonth)
            ? DateTime.Now.ToString("yyyy-MM")
            : yearMonth;

        if (!TryValidateYearMonth(targetMonth, out var message))
            return BadRequest(ApiResponse.Error(400, message));

        var wallet = await _service.GetWallet(studentId, targetMonth);
        return Ok(ApiResponse.Ok(wallet));
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
