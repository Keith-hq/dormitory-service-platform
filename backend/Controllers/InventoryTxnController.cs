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

    /// <summary>STU-25：借用共享物品（扣库存）</summary>
    [HttpPost("shared-items/{itemId}/borrow")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<object>>> Borrow(int itemId)
    {
        var studentId = await ResolveStudentId();

        var (rc, loanId) = await _service.BorrowItem(itemId, studentId);

        var msgs = new[] { "借用成功", "物品不存在", "物品已停用", "库存不足", "信用分不足（低于60）" };
        var msg = rc >= 0 && rc < msgs.Length ? msgs[rc] : "未知错误";
        return rc == 0
            ? Ok(ApiResponse.Ok(new { loanId }, msg))
            : Ok(ApiResponse.Error(400, msg));
    }

    /// <summary>STU-26：归还共享物品</summary>
    [HttpPost("item-loans/{loanId}/return")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<object>>> Return(int loanId)
    {
        var rc = await _service.ReturnItem(loanId);
        return rc == 0
            ? Ok(ApiResponse.Ok(new { }, "归还成功"))
            : Ok(ApiResponse.Error(400, "借出记录不存在或已归还"));
    }

    /// <summary>STU-27：查询当前学生借还记录</summary>
    [HttpGet("item-loans")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<object>>> GetItemLoans()
    {
        var studentId = await ResolveStudentId();
        var loans = await _service.GetItemLoans(studentId);
        return Ok(ApiResponse.Ok(loans));
    }

    // ==================== 宿管端 ====================

    /// <summary>DORM-29：耗材出库</summary>
    [HttpPost("repair-materials/{materialId}/consume")]
    [Authorize(Policy = AuthPolicies.DormAdmin)]
    public async Task<ActionResult<ApiResponse<object>>> ConsumeMaterial(
        int materialId, [FromBody] ConsumeMaterialRequest req)
    {
        var rc = await _service.ConsumeMaterial(materialId, req.TicketId, req.Quantity);
        var msgs = new[] { "出库成功", "耗材不存在", "库存不足" };
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

/// <summary>耗材出库请求体</summary>
public class ConsumeMaterialRequest
{
    /// <summary>报修工单 ID</summary>
    [Range(1, int.MaxValue, ErrorMessage = "工单ID必须大于0")]
    public int TicketId { get; set; }

    /// <summary>消耗数量</summary>
    [Range(1, int.MaxValue, ErrorMessage = "消耗数量必须大于0")]
    public int Quantity { get; set; }
}
