using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Oracle.ManagedDataAccess.Client;
using TemplateDormApi.Data;
using TemplateDormApi.DTO;
using TemplateDormApi.Exceptions;
using TemplateDormApi.Models;

namespace TemplateDormApi.Services;

/// <summary>
/// 水电账单服务实现（难点② 账单端点）。
/// 发布状态机：'未发布' → '已发布'（发布自动触发该月分摊，DORM-21 契约语义）。
/// 修改走条件 UPDATE（WHERE NOT EXISTS 分摊明细），并发分摊竞态由 SQL%ROWCOUNT 守门；
/// 发布走条件 UPDATE（WHERE Publish_Status='未发布'），并发发布竞态同守门。
/// </summary>
public class UtilityFeeService : IUtilityFeeService
{
    private readonly AppDbContext _context;
    private readonly IFeeSharingService _feeSharingService;

    public UtilityFeeService(AppDbContext context, IFeeSharingService feeSharingService)
    {
        _context = context;
        _feeSharingService = feeSharingService;
    }

    public async Task<long> CreateBill(CreateUtilityFeeRequest request)
    {
        // 必填兜底（PR #58 P2 整改）：DTO 已 [Required]，这里拦截绕过模型绑定的直接调用
        if (request.WaterFee is null || request.ElecFee is null)
        {
            throw new BusinessException(400, "waterFee 与 elecFee 为必填字段");
        }

        // 快路径校验：房间存在性 + 同(房间,账期)唯一性
        // 四审真实 Oracle 实测（IT-C3-001 执行现场）：顶层 AnyAsync 被 provider
        // 翻译为 CASE WHEN EXISTS(...) THEN True ELSE False，Oracle 21c 无布尔
        // 字面量 → ORA-00904。改用 CountAsync（翻译为 COUNT(*)）。
        var roomExists = await _context.Rooms.CountAsync(r => r.RoomId == request.RoomId) > 0;
        if (!roomExists)
        {
            throw new BusinessException(404, "房间不存在", StatusCodes.Status404NotFound);
        }

        var duplicate = await _context.UtilityFees
            .CountAsync(f => f.RoomId == request.RoomId && f.YearMonth == request.YearMonth) > 0;
        if (duplicate)
        {
            throw new BusinessException(400, "该房间该月份已存在账单");
        }

        var bill = new UtilityFee
        {
            RoomId = request.RoomId,
            YearMonth = request.YearMonth,
            WaterFee = request.WaterFee,
            PowerFee = request.ElecFee,   // 契约字段 elecFee ↔ 表列 Power_Fee
            IsPaid = "否",
            PublishStatus = "未发布"
        };

        _context.UtilityFees.Add(bill);
        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException ex)
        {
            // 并发兜底：UK_D_UTILITY_FEE_ROOM_MONTH → 重复账单；FK_D_UTILITY_FEE_ROOM → 房间不存在
            if (HasOracleNumber(ex, 1))
            {
                throw new BusinessException(400, "该房间该月份已存在账单");
            }

            if (HasOracleNumber(ex, 2291))
            {
                throw new BusinessException(404, "房间不存在", StatusCodes.Status404NotFound);
            }

            throw;
        }

        return bill.FeeId;  // 由 SEQ_D_UTILITY_FEE_ID 触发器回填（迁移 026）
    }

    public async Task UpdateBill(long feeId, UpdateUtilityFeeRequest request)
    {
        var bill = await _context.UtilityFees.FindAsync(feeId)
            ?? throw new BusinessException(404, "账单不存在", StatusCodes.Status404NotFound);

        // DORM-20 语义（PR #58 P2 整改）：契约"已发布且学生已缴费的账单禁止修改"——
        // 已发布但【未分摊】的账单允许修改（发布后改金额再分摊不产生脱节）；
        // 一旦存在分摊明细即禁止（含"已分摊未缴"——此时改金额会让账单与明细脱节，
        // 比契约更严格的口径，PR 描述说明）。
        var hasDetails = await _context.FeeDetails.CountAsync(d => d.FeeId == feeId) > 0;
        if (hasDetails)
        {
            throw new BusinessException(400, "已分摊或已缴的账单不可修改");
        }

        // 条件 UPDATE：WHERE NOT EXISTS(明细) 与并发分摊线性化——
        // 快路径校验与 UPDATE 之间若被并发分摊抢先，ROWCOUNT=0 返回 400
        var rows = await _context.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE D_Utility_Fee SET Water_Fee = {request.WaterFee}, Power_Fee = {request.ElecFee} WHERE Fee_ID = {feeId} AND NOT EXISTS (SELECT 1 FROM D_Fee_Detail d WHERE d.Fee_ID = {feeId})");

        if (rows == 0)
        {
            throw new BusinessException(400, "已分摊或已缴的账单不可修改");
        }
    }

    public async Task PublishBill(long feeId)
    {
        var bill = await _context.UtilityFees.FindAsync(feeId)
            ?? throw new BusinessException(404, "账单不存在", StatusCodes.Status404NotFound);

        var rows = await _context.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE D_Utility_Fee SET Publish_Status = '已发布' WHERE Fee_ID = {feeId} AND Publish_Status = '未发布'");

        if (rows == 0)
        {
            throw new BusinessException(400, "账单已发布");
        }

        // DORM-21 契约 summary"发布账单（触发分摊）"（PR #58 P2 整改）：
        // 发布成功后自动触发该账期整月分摊（SP 自 COMMIT 幂等，重复触发安全）；
        // 分摊异常不影响已提交的发布状态，可重试 POST /{feeId}/allocate 补分摊。
        await _feeSharingService.CalcMonthlyFee(bill.YearMonth);
    }

    public async Task<AllocateResultDto> AllocateBill(long feeId)
    {
        var bill = await _context.UtilityFees.FindAsync(feeId)
            ?? throw new BusinessException(404, "账单不存在", StatusCodes.Status404NotFound);

        if (bill.PublishStatus != "已发布")
        {
            throw new BusinessException(400, "账单未发布，不能分摊");
        }

        // 整月维度分摊：SP_Calc_Monthly_Fee 自 COMMIT 且幂等（COUNT 预检查 +
        // UK(Fee_ID, Student_ID) 兜底），重复触发不产生重复明细
        await _feeSharingService.CalcMonthlyFee(bill.YearMonth);

        var detailCount = await _context.FeeDetails.CountAsync(f => f.FeeId == bill.FeeId);
        return new AllocateResultDto
        {
            FeeId = bill.FeeId,
            YearMonth = bill.YearMonth,
            DetailCount = detailCount
        };
    }

    public async Task<UtilityFeeDetailsDto> GetBillDetails(long feeId)
    {
        var bill = await _context.UtilityFees.FindAsync(feeId)
            ?? throw new BusinessException(404, "账单不存在", StatusCodes.Status404NotFound);

        var items = await _context.FeeDetails
            .Where(f => f.FeeId == bill.FeeId)
            .OrderBy(f => f.StudentId)
            .Select(f => new UtilityFeeDetailItemDto
            {
                DetailId = f.DetailId,
                StudentId = f.StudentId,
                RoomId = f.RoomId,
                StayDays = f.StayDays,
                TotalDays = f.TotalDays,
                WaterShare = f.WaterShare,
                PowerShare = f.PowerShare,
                Total = f.WaterShare + f.PowerShare,
                BillType = f.BillType,
                IsPaid = f.IsPaid
            })
            .ToListAsync();

        return new UtilityFeeDetailsDto
        {
            FeeId = bill.FeeId,
            RoomId = bill.RoomId,
            YearMonth = bill.YearMonth,
            Items = items
        };
    }

    public async Task<PagedResult<UtilityFeeListItemDto>> GetBills(
        long? buildingId, string? yearMonth, bool? isPaid, string? publishStatus, int page, int pageSize)
    {
        if (publishStatus is not null && publishStatus != "未发布" && publishStatus != "已发布")
        {
            throw new BusinessException(400, "publishStatus 只能为 未发布 或 已发布");
        }

        var query = _context.UtilityFees.AsQueryable();

        // 楼栋过滤：D_Room.Building_ID 关联（EXISTS）
        if (buildingId.HasValue)
        {
            query = query.Where(f => _context.Rooms
                .Any(r => r.RoomId == f.RoomId && r.BuildingId == buildingId.Value));
        }

        if (!string.IsNullOrWhiteSpace(yearMonth))
        {
            query = query.Where(f => f.YearMonth == yearMonth);
        }

        if (publishStatus is not null)
        {
            query = query.Where(f => f.PublishStatus == publishStatus);
        }

        // 缴费状态过滤（P1-3 契约字段）：以 D_Fee_Detail 为权威——
        // true=无未缴明细（全部缴清，无明细视为缴清）；false=存在未缴明细。
        // 不用账单行 Is_Paid 列（建单时的快照，SP 只更新明细不回收账单行）。
        if (isPaid.HasValue)
        {
            if (isPaid.Value)
            {
                query = query.Where(f =>
                    _context.FeeDetails.Count(d => d.FeeId == f.FeeId && d.IsPaid == "否") == 0);
            }
            else
            {
                query = query.Where(f =>
                    _context.FeeDetails.Count(d => d.FeeId == f.FeeId && d.IsPaid == "否") > 0);
            }
        }

        var total = await query.CountAsync();

        var items = await query
            .OrderByDescending(f => f.YearMonth)
            .ThenBy(f => f.RoomId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(f => new UtilityFeeListItemDto
            {
                FeeId = f.FeeId,
                RoomId = f.RoomId,
                YearMonth = f.YearMonth,
                WaterFee = f.WaterFee,
                PowerFee = f.PowerFee,
                // 与缴费状态过滤同口径（明细为权威）：账单行 Is_Paid 是建单时快照，
                // SP 只更新明细不回收账单行，直接投影会与 IT-C3-001 判定④
                // 「账单 isPaid=已缴」脱节（实测现场发现）
                IsPaid = _context.FeeDetails.Count(d => d.FeeId == f.FeeId && d.IsPaid == "否") == 0 ? "是" : "否",
                PublishStatus = f.PublishStatus
            })
            .ToListAsync();

        return new PagedResult<UtilityFeeListItemDto>
        {
            Items = items,
            Total = total,
            Page = page,
            PageSize = pageSize
        };
    }

    /// <summary>沿异常链查找指定 ORA 错误号（复制 CreditService.HasOracleNumber 模式）</summary>
    private static bool HasOracleNumber(Exception exception, int number)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is OracleException oracleException && oracleException.Number == number)
            {
                return true;
            }
        }

        return false;
    }
}
