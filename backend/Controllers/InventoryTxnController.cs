using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using TemplateDormApi.Data;
using TemplateDormApi.DTO;
using TemplateDormApi.Exceptions;
using TemplateDormApi.Security;
using TemplateDormApi.Services;

namespace TemplateDormApi.Controllers;

/// <summary>
/// 共享物品借还与耗材出库接口（难点④）。
/// STU-24~27 学生端，DORM-29~30 宿管端。
/// </summary>
[ApiController]
[Route("api")]
public class InventoryTxnController : ControllerBase
{
    private readonly IInventoryTxnService _service;
    private readonly AppDbContext _context;

    public InventoryTxnController(IInventoryTxnService service, AppDbContext context)
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
    /// 幂等键 100 字符边界校验（五审）：迁移 019 列宽为 VARCHAR2(100 CHAR)，
    /// 校验口径随 CHAR 语义统一为字符数（key.Length），超长在入口拒绝，避免 Oracle 500。
    /// </summary>
    private static bool IsIdempotencyKeyTooLong(string? key)
    {
        return !string.IsNullOrEmpty(key) && key.Length > 100;
    }

    // ==================== 学生端 ====================

    /// <summary>STU-24：查询可借共享物品列表</summary>
    [HttpGet("shared-items")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<object>>> GetSharedItems(
        [FromQuery] int? buildingId = null)
    {
        var items = await _service.GetSharedItems(buildingId);
        return Ok(ApiResponse.Ok(items));
    }

    /// <summary>STU-25：借用共享物品（扣库存，需 Idempotency-Key 保证幂等）</summary>
    [HttpPost("item-loans")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<object>>> Borrow(
        [FromBody] BorrowItemRequest req)
    {
        var studentId = await ResolveStudentId();
        var idempotencyKey = GetIdempotencyKey(Request);

        if (string.IsNullOrWhiteSpace(idempotencyKey))
            return Ok(ApiResponse.Error(400, "缺少 Idempotency-Key 请求头"));

        if (IsIdempotencyKeyTooLong(idempotencyKey))
            return Ok(ApiResponse.Error(400, "Idempotency-Key 不能超过 100 字符"));

        var (rc, loanId) = await _service.BorrowItem(req.ItemId, studentId, idempotencyKey);

        var msgs = new[]
        {
            "借用成功", "物品不存在", "物品已停用", "库存不足", "信用分不足（低于60）",
            "Idempotency-Key 已被使用且请求内容不一致", "Idempotency-Key 不能超过 100 字符"
        };
        var msg = rc >= 0 && rc < msgs.Length ? msgs[rc] : "未知错误";
        return rc == 0
            ? Ok(ApiResponse.Ok(new { loanId }, msg))
            : Ok(ApiResponse.Error(400, msg));
    }

    /// <summary>STU-26：归还共享物品（校验 Student_ID 归属，超期归还按次扣 2 分）</summary>
    [HttpPost("item-loans/{loanId}/return")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<object>>> Return(int loanId)
    {
        var studentId = await ResolveStudentId();
        var (rc, creditPending) = await _service.ReturnItem(loanId, studentId);

        // 四审 P1-2：归还已生效但扣分未完成时，归还结果必须如实成功，
        // 只透出"待补偿"状态（巡检自愈重试），不能伪装成归还失败
        var suffix = creditPending ? "；超期扣分待补偿，系统将自动重试" : "";
        return rc == 0
            ? Ok(ApiResponse.Ok(new { creditPending }, "归还成功" + suffix))
            : Ok(ApiResponse.Error(400, "借出记录不存在、已归还或非本人操作" + suffix));
    }

    /// <summary>STU-27：查询学生借还记录（分页，仅本人可见）</summary>
    [HttpGet("students/{studentId}/item-loans")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<object>>> GetItemLoans(
        string studentId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        var currentStudentId = await ResolveStudentId();
        if (!string.Equals(studentId, currentStudentId, StringComparison.Ordinal))
            return Ok(ApiResponse.Error(403, "无权查看他人的借还记录"));

        if (page < 1 || pageSize < 1 || pageSize > 100)
            return Ok(ApiResponse.Error(400, "分页参数不合法：page 从 1 开始，pageSize 范围 1~100"));

        var result = await _service.GetItemLoans(studentId, page, pageSize);
        return Ok(ApiResponse.Ok(result));
    }

    // ==================== 宿管端 ====================

    /// <summary>DORM-29：耗材出库（需 Idempotency-Key 保证幂等）</summary>
    [HttpPost("repair-tickets/{ticketId}/materials")]
    [Authorize(Policy = AuthPolicies.DormAdmin)]
    public async Task<ActionResult<ApiResponse<object>>> ConsumeMaterial(
        int ticketId, [FromBody] ConsumeMaterialRequest req)
    {
        var idempotencyKey = GetIdempotencyKey(Request);

        if (string.IsNullOrWhiteSpace(idempotencyKey))
            return Ok(ApiResponse.Error(400, "缺少 Idempotency-Key 请求头"));

        if (IsIdempotencyKeyTooLong(idempotencyKey))
            return Ok(ApiResponse.Error(400, "Idempotency-Key 不能超过 100 字符"));

        var rc = await _service.ConsumeMaterial(req.MaterialId, ticketId, req.Quantity, idempotencyKey);
        var msgs = new[] { "出库成功", "耗材不存在", "库存不足", "Idempotency-Key 已被使用且请求内容不一致", "Idempotency-Key 不能超过 100 字符" };
        var msg = rc >= 0 && rc < msgs.Length ? msgs[rc] : "未知错误";
        return rc == 0
            ? Ok(ApiResponse.Ok(new { }, msg))
            : Ok(ApiResponse.Error(400, msg));
    }

    /// <summary>DORM-30：耗材库存查询</summary>
    [HttpGet("repair-materials")]
    [Authorize(Policy = AuthPolicies.DormAdmin)]
    public async Task<ActionResult<ApiResponse<object>>> GetRepairMaterials()
    {
        var materials = await _service.GetRepairMaterials();
        return Ok(ApiResponse.Ok(materials));
    }

    // ==================== Internal ====================

    /// <summary>逾期巡检（Internal + ServiceKeyAuth）</summary>
    [HttpPost("internal/scheduler/check-overdue")]
    [ServiceKeyAuth]
    public async Task<ActionResult<ApiResponse<object>>> CheckOverdue()
    {
        await _service.CheckOverdue();
        return Ok(ApiResponse.Ok(new { }, "逾期巡检完成"));
    }
}

/// <summary>借用物品请求体</summary>
public class BorrowItemRequest
{
    /// <summary>共享物品 ID</summary>
    [Range(1, int.MaxValue, ErrorMessage = "物品ID必须大于0")]
    public int ItemId { get; set; }
}

/// <summary>耗材出库请求体</summary>
public class ConsumeMaterialRequest
{
    /// <summary>耗材 ID</summary>
    [Range(1, int.MaxValue, ErrorMessage = "耗材ID必须大于0")]
    public int MaterialId { get; set; }

    /// <summary>消耗数量</summary>
    [Range(1, int.MaxValue, ErrorMessage = "消耗数量必须大于0")]
    public int Quantity { get; set; }
}
