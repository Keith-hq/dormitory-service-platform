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
/// 发布状态机：'未发布' → '已发布'。修改与发布均走条件 UPDATE
/// （WHERE Publish_Status='未发布'），并发发布竞态由 SQL%ROWCOUNT 守门。
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
        // 快路径校验：房间存在性 + 同(房间,账期)唯一性
        var roomExists = await _context.Rooms.AnyAsync(r => r.RoomId == request.RoomId);
        if (!roomExists)
        {
            throw new BusinessException(404, "房间不存在", StatusCodes.Status404NotFound);
        }

        var duplicate = await _context.UtilityFees
            .AnyAsync(f => f.RoomId == request.RoomId && f.YearMonth == request.YearMonth);
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

        return bill.FeeId;  // 由 SEQ_D_UTILITY_FEE_ID 触发器回填（迁移 023）
    }

    public async Task UpdateBill(long feeId, UpdateUtilityFeeRequest request)
    {
        var bill = await _context.UtilityFees.FindAsync(feeId)
            ?? throw new BusinessException(404, "账单不存在", StatusCodes.Status404NotFound);

        if (bill.PublishStatus != "未发布")
        {
            throw new BusinessException(400, "已发布的账单不可修改");
        }

        // 条件 UPDATE：WHERE Publish_Status='未发布' 防发布竞态——
        // 快路径校验与 UPDATE 之间若被并发发布抢先，ROWCOUNT=0 返回 400
        var rows = await _context.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE D_Utility_Fee SET Water_Fee = {request.WaterFee}, Power_Fee = {request.ElecFee} WHERE Fee_ID = {feeId} AND Publish_Status = '未发布'");

        if (rows == 0)
        {
            throw new BusinessException(400, "已发布的账单不可修改");
        }
    }

    public async Task PublishBill(long feeId)
    {
        var exists = await _context.UtilityFees.AnyAsync(f => f.FeeId == feeId);
        if (!exists)
        {
            throw new BusinessException(404, "账单不存在", StatusCodes.Status404NotFound);
        }

        var rows = await _context.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE D_Utility_Fee SET Publish_Status = '已发布' WHERE Fee_ID = {feeId} AND Publish_Status = '未发布'");

        if (rows == 0)
        {
            throw new BusinessException(400, "账单已发布");
        }
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

    public async Task<List<UtilityFeeListItemDto>> GetBills(string? yearMonth, string? publishStatus)
    {
        if (publishStatus is not null && publishStatus != "未发布" && publishStatus != "已发布")
        {
            throw new BusinessException(400, "publishStatus 只能为 未发布 或 已发布");
        }

        var query = _context.UtilityFees.AsQueryable();

        if (!string.IsNullOrWhiteSpace(yearMonth))
        {
            query = query.Where(f => f.YearMonth == yearMonth);
        }

        if (publishStatus is not null)
        {
            query = query.Where(f => f.PublishStatus == publishStatus);
        }

        return await query
            .OrderByDescending(f => f.YearMonth)
            .ThenBy(f => f.RoomId)
            .Select(f => new UtilityFeeListItemDto
            {
                FeeId = f.FeeId,
                RoomId = f.RoomId,
                YearMonth = f.YearMonth,
                WaterFee = f.WaterFee,
                PowerFee = f.PowerFee,
                IsPaid = f.IsPaid,
                PublishStatus = f.PublishStatus
            })
            .ToListAsync();
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
