using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TemplateDormApi.Data;
using TemplateDormApi.DTO;
using TemplateDormApi.Security;
using TemplateDormApi.Services;

namespace TemplateDormApi.Controllers;

[ApiController]
[Authorize(Roles = AuthPolicies.SuperAdmin)]
[Route("api/reports")]
public class ReportsController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IAuditService _auditService;
    private readonly ILogger<ReportsController> _logger;

    public ReportsController(
        AppDbContext context,
        IAuditService auditService,
        ILogger<ReportsController> logger)
    {
        _context = context;
        _auditService = auditService;
        _logger = logger;
    }

    // GET /reports/{type} - 统计报表（REPT-01）
    // 支持 6 类报表：occupancy, utility, lateReturn, repair, violation, leave
    //（package 快递报表随快递业务放弃下线，契约残留条目按 C-049 记"不交付"。）
    [HttpGet("{type}")]
    public async Task<IActionResult> GetReport(string type, [FromQuery] ReportQueryDto query)
    {
        // 校验报表类型
        var validTypes = new[] { "occupancy", "utility", "lateReturn", "repair", "violation", "leave" };
        if (!validTypes.Contains(type))
            return BadRequest(ApiResponse.Error(400, $"无效的报表类型，支持：{string.Join(", ", validTypes)}"));

        // 解析月份参数（格式：yyyy-MM）
        if (string.IsNullOrEmpty(query.YearMonth) || !query.YearMonth.Contains('-'))
            return BadRequest(ApiResponse.Error(400, "请提供月份参数，格式：yyyy-MM (如 2026-08)"));

        var yearMonth = query.YearMonth.Trim();
        var parts = yearMonth.Split('-');
        if (parts.Length != 2 || !int.TryParse(parts[0], out int year) || !int.TryParse(parts[1], out int month))
            return BadRequest(ApiResponse.Error(400, "月份格式无效，请使用 yyyy-MM 格式"));

        // 构建报表数据（根据类型调用不同逻辑）
        object? reportData = type switch
        {
            "occupancy" => await GetOccupancyReport(year, month),
            "utility" => await GetUtilityReport(year, month),
            "lateReturn" => await GetLateReturnReport(year, month),
            "repair" => await GetRepairReport(year, month),
            "violation" => await GetViolationReport(year, month),
            "leave" => await GetLeaveReport(year, month),
            _ => null
        };

        if (reportData == null)
            return Ok(ApiResponse.Ok(new { message = "该月份暂无数据", data = new { } }));

        // 审计日志（记录报表生成）
        await _auditService.LogEventAsync(
            eventType: $"GET /reports/{type}",
            targetType: "Report",
            targetId: yearMonth,
            actorAccountId: GetCurrentUserId(),
            details: $"生成 {type} 报表，月份：{yearMonth}"
        );

        return Ok(ApiResponse.Ok(new { month = yearMonth, reportData }));
    }

    // ===== 各报表查询方法 =====

    // 1. 入住率报表：按楼栋统计房间总数、已入住数、空置数、入住率
    private async Task<object> GetOccupancyReport(int year, int month)
    {
        var buildingStats = await _context.Buildings
            .Select(b => new
            {
                b.BuildingId,
                b.BuildingName,
                TotalRooms = b.Rooms.Count(),
                OccupiedRooms = b.Rooms.Count(r => r.Occupancy > 0),
                EmptyRooms = b.Rooms.Count(r => r.Occupancy == 0)
            })
            .ToListAsync();

        return buildingStats.Select(b => new
        {
            b.BuildingId,
            b.BuildingName,
            b.TotalRooms,
            b.OccupiedRooms,
            b.EmptyRooms,
            OccupancyRate = b.TotalRooms > 0
                ? Math.Round((double)b.OccupiedRooms / b.TotalRooms * 100, 2)
                : 0
        });
    }

    // 2. 水电收缴报表：按楼栋统计水电费总额、已缴额、收缴率
    //    （041 起账单头不再有 Is_Paid，已缴额按明细级 D_Fee_Detail.Is_Paid 聚合，
    //      与缴费状态唯一事实来源口径一致）
    //    口径注记（REPT-01 utility，与 GetBills 展示口径不同，勿混用）：
    //      - TotalFees = 账单头金额和（含未发布账单，应收口径按发布状态另算）；
    //      - PaidFees = 已缴明细金额和（无明细的账单记 0，属收缴统计口径；
    //        GetBills 列表按「无未缴明细即视为缴清」展示，两者不等价是有意的）。
    private async Task<object> GetUtilityReport(int year, int month)
    {
        var yearMonth = $"{year}-{month:D2}";
        var utilities = await _context.UtilityFees
            .Where(u => u.YearMonth == yearMonth)
            .Include(u => u.Room!)
                .ThenInclude(r => r.Building)
            .ToListAsync();

        var feeIds = utilities.Select(u => u.FeeId).ToList();
        var paidByFee = await _context.FeeDetails
            .Where(d => feeIds.Contains(d.FeeId) && d.IsPaid == "是")
            .GroupBy(d => d.FeeId)
            .Select(g => new { FeeId = g.Key, Paid = g.Sum(d => d.WaterShare + d.PowerShare) })
            .ToDictionaryAsync(x => x.FeeId, x => x.Paid);

        var buildingStats = utilities
            .GroupBy(u => new { u.Room!.BuildingId, BuildingName = u.Room!.Building!.BuildingName })
            .Select(g => new
            {
                g.Key.BuildingId,
                g.Key.BuildingName,
                TotalFees = g.Sum(u => (u.WaterFee ?? 0) + (u.PowerFee ?? 0)),
                PaidFees = g.Sum(u => paidByFee.TryGetValue(u.FeeId, out var paid) ? paid : 0m)
            })
            .Select(g => new
            {
                g.BuildingId,
                g.BuildingName,
                g.TotalFees,
                g.PaidFees,
                CollectionRate = g.TotalFees > 0
                    ? Math.Round((double)(g.PaidFees / g.TotalFees * 100), 2)
                    : 0
            })
            .ToList();

        return buildingStats;
    }

    // 3. 晚归报表：按月份统计晚归记录数、涉及学生数
    private async Task<object> GetLateReturnReport(int year, int month)
    {
        var startDate = new DateTime(year, month, 1);
        var endDate = startDate.AddMonths(1);

        var lateEntries = await _context.LateEntries
            .Where(l => l.ReturnTime >= startDate && l.ReturnTime < endDate)
            .ToListAsync();

        var studentIds = lateEntries.Select(l => l.StudentId).Distinct().Count();

        return new
        {
            TotalLateEntries = lateEntries.Count,
            DistinctStudents = studentIds,
            LateEntryPerStudent = studentIds > 0
                ? Math.Round((double)lateEntries.Count / studentIds, 2)
                : 0
        };
    }

    // 4. 报修报表：统计各状态工单数
    private async Task<object> GetRepairReport(int year, int month)
    {
        var startDate = new DateTime(year, month, 1);
        var endDate = startDate.AddMonths(1);

        var tickets = await _context.RepairTickets
            .Where(r => r.SubmitTime >= startDate && r.SubmitTime < endDate)
            .ToListAsync();

        return new
        {
            TotalTickets = tickets.Count,
            ByStatus = tickets
                .GroupBy(r => r.Status ?? "未知")
                .Select(g => new { Status = g.Key, Count = g.Count() })
                .ToList()
        };
    }

    // 5. 违规报表：统计各类型违规数量
    private async Task<object> GetViolationReport(int year, int month)
    {
        var startDate = new DateTime(year, month, 1);
        var endDate = startDate.AddMonths(1);

        var violations = await _context.ViolationRecords
            .Where(v => v.VioDate >= startDate && v.VioDate < endDate)
            .ToListAsync();

        return new
        {
            TotalViolations = violations.Count,
            ByType = violations
                .GroupBy(v => v.VioType)
                .Select(g => new { VioType = g.Key, Count = g.Count() })
                .ToList()
        };
    }

    // 6. 离校报表：统计各状态离校申请数
    private async Task<object> GetLeaveReport(int year, int month)
    {
        var startDate = new DateTime(year, month, 1);
        var endDate = startDate.AddMonths(1);

        var leaves = await _context.LeaveApplications
            .Where(l => l.LeaveDate >= startDate && l.LeaveDate < endDate)
            .ToListAsync();

        return new
        {
            TotalApplications = leaves.Count,
            ByStatus = leaves
                .GroupBy(l => l.Status ?? "未知")
                .Select(g => new { Status = g.Key, Count = g.Count() })
                .ToList()
        };
    }

    // ===== DTO 定义 =====
    public class ReportQueryDto
    {
        public string YearMonth { get; set; } = string.Empty; // 格式：yyyy-MM
    }

    // ===== 辅助方法 =====
    private int? GetCurrentUserId()
    {
        var claim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        return int.TryParse(claim, out var id) ? id : null;
    }
}