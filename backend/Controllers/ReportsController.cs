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
    // 支持 7 类报表：occupancy, utility, parcel, lateentry, repair, violation, leave
    [HttpGet("{type}")]
    public async Task<IActionResult> GetReport(string type, [FromQuery] ReportQueryDto query)
    {
        // 校验报表类型
        var validTypes = new[] { "occupancy", "utility", "package", "lateReturn", "repair", "violation", "leave" };
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
            "package" => await GetPackageReport(year, month),
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
    private async Task<object> GetUtilityReport(int year, int month)
    {
        var yearMonth = $"{year}-{month:D2}";
        var utilities = await _context.UtilityFees
            .Where(u => u.YearMonth == yearMonth)
            .Include(u => u.Room!)
                .ThenInclude(r => r.Building)
            .ToListAsync();

        var buildingStats = utilities
            .GroupBy(u => new { u.Room!.BuildingId, BuildingName = u.Room!.Building!.BuildingName })
            .Select(g => new
            {
                g.Key.BuildingId,
                g.Key.BuildingName,
                TotalFees = g.Sum(u => (u.WaterFee ?? 0) + (u.PowerFee ?? 0)),
                PaidFees = g.Where(u => u.IsPaid == "是").Sum(u => (u.WaterFee ?? 0) + (u.PowerFee ?? 0))
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

    // 3. 快递报表：按楼栋统计快递总数、已取件数、取件率
    private async Task<object> GetPackageReport(int year, int month)
    {
        var startDate = new DateTime(year, month, 1);
        var endDate = startDate.AddMonths(1);

        var parcels = await _context.ParcelRecords
            .Where(p => p.ArriveTime >= startDate && p.ArriveTime < endDate)
            .Include(p => p.Student)
            .ToListAsync();

        // 由于 ParcelRecord 没有 BuildingId，需关联 Student 或 BedAllocation 获取楼栋
        // 简化：直接统计总数和取件数
        var total = parcels.Count;
        var pickedUp = parcels.Count(p => p.PickupTime != null);

        return new
        {
            TotalParcels = total,
            PickedUp = pickedUp,
            PendingPickup = total - pickedUp,
            PickupRate = total > 0 ? Math.Round((double)pickedUp / total * 100, 2) : 0
        };
    }

    // 4. 晚归报表：按月份统计晚归记录数、涉及学生数
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

    // 5. 报修报表：统计各状态工单数
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

    // 6. 违规报表：统计各类型违规数量
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

    // 7. 离校报表：统计各状态离校申请数
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