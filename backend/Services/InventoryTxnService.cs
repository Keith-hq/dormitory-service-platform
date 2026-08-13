using Microsoft.EntityFrameworkCore;
using Oracle.ManagedDataAccess.Client;
using System.Data;
using TemplateDormApi.Data;
using TemplateDormApi.DTO;
using TemplateDormApi.Exceptions;
using TemplateDormApi.Models;

namespace TemplateDormApi.Services;

/// <summary>
/// 共享物品借还与耗材出库服务实现（难点④）。
/// 库存扣减走 PL/SQL 存储过程的乐观锁（WHERE Available_Qty > 0 / Stock_Qty >= n + SQL%ROWCOUNT）；
/// 信用分扣分统一走信用分公共服务 ICreditService（Event_Key 幂等 + 学生行锁串行化 + 冻结通知），
/// 归还与逾期巡检并发时只有归还路径写信用分，配合 Event_Key 唯一约束只产生一次扣分。
/// </summary>
public class InventoryTxnService : IInventoryTxnService
{
    private readonly AppDbContext _context;
    private readonly ICreditService _creditService;

    public InventoryTxnService(AppDbContext context, ICreditService creditService)
    {
        _context = context;
        _creditService = creditService;
    }

    // ===== 写操作：调用存储过程 =====

    public async Task<(int resultCode, int loanId)> BorrowItem(int itemId, string studentId, string? idempotencyKey)
    {
        var conn = _context.Database.GetDbConnection();
        var wasOpen = conn.State == ConnectionState.Open;
        if (!wasOpen) await conn.OpenAsync();

        try
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SP_Borrow_Item";
            cmd.CommandType = CommandType.StoredProcedure;
            cmd.Parameters.Add(new OracleParameter("p_Item_ID", itemId));
            cmd.Parameters.Add(new OracleParameter("p_Student_ID", studentId));
            cmd.Parameters.Add(new OracleParameter("p_Idempotency_Key",
                (object?)idempotencyKey ?? DBNull.Value));

            var rc = new OracleParameter("p_Result_Code", OracleDbType.Int32, ParameterDirection.Output);
            var lid = new OracleParameter("p_Loan_ID", OracleDbType.Int32, ParameterDirection.Output);
            cmd.Parameters.Add(rc);
            cmd.Parameters.Add(lid);

            await cmd.ExecuteNonQueryAsync();

            int code = OracleValueToInt(rc.Value);
            int loanId = OracleValueToInt(lid.Value);
            return (code, loanId);
        }
        finally
        {
            if (!wasOpen) conn.Close();
        }
    }

    public async Task<int> ReturnItem(int loanId, string studentId)
    {
        var (rc, overdueDays) = await CallReturnSp(loanId, studentId);

        if (rc == 0)
        {
            // 超期归还按 PRD 按次扣 2 分（组长确认），扣分走信用分统一入口
            if (overdueDays > 0)
            {
                await DeductOverdueOnceAsync(loanId, studentId, overdueDays);
            }
            return 0;
        }

        // rc=1（不存在/已归还/非本人）补偿路径：SP 成功但应用在扣分前崩溃时，
        // 客户端重试归还得到 rc=1；若记录确属本人、已归还且逾期，补上未完成的扣分。
        // Event_Key 幂等保证最多扣一次，跨用户重试则不会命中本人条件。
        var loan = await _context.Set<ItemLoan>().AsNoTracking()
            .FirstOrDefaultAsync(l => l.LoanId == loanId && l.StudentId == studentId);
        if (loan is { ReturnTime: not null } && loan.ReturnTime > loan.DueTime)
        {
            var days = (int)(loan.ReturnTime.Value.Date - loan.DueTime.Date).TotalDays;
            await DeductOverdueOnceAsync(loanId, studentId, Math.Max(1, days));
        }
        return 1;
    }

    public async Task<int> ConsumeMaterial(int materialId, int ticketId, int quantity, string? idempotencyKey)
    {
        return await CallResultCode("SP_Consume_Material",
            new OracleParameter("p_Material_ID", materialId),
            new OracleParameter("p_Ticket_ID", ticketId),
            new OracleParameter("p_Quantity", quantity),
            new OracleParameter("p_Idempotency_Key",
                (object?)idempotencyKey ?? DBNull.Value));
    }

    public async Task CheckOverdue()
    {
        await _context.Database.ExecuteSqlRawAsync("BEGIN SP_Check_Overdue; END;");
    }

    // ===== 查询 =====

    public async Task<List<SharedItem>> GetSharedItems(int? buildingId = null)
    {
        if (buildingId.HasValue)
        {
            return await _context.Set<SharedItem>()
                .FromSqlRaw(
                    @"SELECT Item_ID AS ItemId, Item_Name AS ItemName,
                            Building_ID AS BuildingId, Total_Qty AS TotalQty,
                            Available_Qty AS AvailableQty, Status
                     FROM D_Shared_Item
                     WHERE Status = '正常' AND Available_Qty > 0 AND Building_ID = {0}
                     ORDER BY Item_ID", buildingId.Value)
                .ToListAsync();
        }

        return await _context.Set<SharedItem>()
            .FromSqlRaw(
                @"SELECT Item_ID AS ItemId, Item_Name AS ItemName,
                        Building_ID AS BuildingId, Total_Qty AS TotalQty,
                        Available_Qty AS AvailableQty, Status
                 FROM D_Shared_Item
                 WHERE Status = '正常' AND Available_Qty > 0
                 ORDER BY Item_ID")
            .ToListAsync();
    }

    public async Task<PagedResult<ItemLoan>> GetItemLoans(string studentId, int page, int pageSize)
    {
        var sql = @"SELECT Loan_ID AS LoanId, Item_ID AS ItemId, Student_ID AS StudentId,
                        Borrow_Time AS BorrowTime, Due_Time AS DueTime, Return_Time AS ReturnTime
                 FROM D_Item_Loan
                 WHERE Student_ID = {0}
                 ORDER BY Borrow_Time DESC";

        var total = await _context.Database
            .SqlQueryRaw<int>("SELECT COUNT(*) FROM D_Item_Loan WHERE Student_ID = {0}", studentId)
            .FirstOrDefaultAsync();

        var items = await _context.Set<ItemLoan>()
            .FromSqlRaw(sql, studentId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new PagedResult<ItemLoan>
        {
            Items = items,
            Total = total,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<List<RepairMaterial>> GetRepairMaterials()
    {
        return await _context.Set<RepairMaterial>()
            .FromSqlRaw(
                @"SELECT Material_ID AS MaterialId, Material_Name AS MaterialName,
                        Unit, Stock_Qty AS StockQty
                 FROM D_Repair_Material
                 ORDER BY Material_ID")
            .ToListAsync();
    }

    // ===== 私有辅助 =====

    /// <summary>
    /// 超期归还按次扣 2 分（PRD 规则，组长确认）：统一走信用分公共服务。
    /// Event_Key = OVERDUE-{loanId}：与巡检并发、客户端重试都只产生一次扣分，
    /// 且冻结（跌破 60）通知由信用分服务统一发出，语义不丢失。
    /// 分数已为 0 或学生账户已停用时跳过（无可扣分数/无扣分对象）。
    /// </summary>
    private async Task DeductOverdueOnceAsync(int loanId, string studentId, int overdueDays)
    {
        try
        {
            var status = await _creditService.GetStatusAsync(studentId, CancellationToken.None);
            if (status.CurrentScore <= 0)
            {
                return;
            }
        }
        catch (BusinessException ex) when (ex.Code == 40401)
        {
            return; // 学生账户不存在或已停用，跳过扣分
        }

        await _creditService.DeductAsync(new CreditDeductDto
        {
            StudentId = studentId,
            ScoreChange = -2,
            Reason = $"共享物品超期归还（Loan_ID={loanId}，逾期{overdueDays}天，按次扣2分）",
            EventKey = $"OVERDUE-{loanId}"
        }, CancellationToken.None);
    }

    /// <summary>调用 SP_Return_Item，返回 (resultCode, overdueDays)</summary>
    private async Task<(int rc, int overdueDays)> CallReturnSp(int loanId, string studentId)
    {
        var conn = _context.Database.GetDbConnection();
        var wasOpen = conn.State == ConnectionState.Open;
        if (!wasOpen) await conn.OpenAsync();

        try
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SP_Return_Item";
            cmd.CommandType = CommandType.StoredProcedure;
            cmd.Parameters.Add(new OracleParameter("p_Loan_ID", loanId));
            cmd.Parameters.Add(new OracleParameter("p_Student_ID", studentId));

            var rc = new OracleParameter("p_Result_Code", OracleDbType.Int32, ParameterDirection.Output);
            var days = new OracleParameter("p_Overdue_Days", OracleDbType.Int32, ParameterDirection.Output);
            cmd.Parameters.Add(rc);
            cmd.Parameters.Add(days);

            await cmd.ExecuteNonQueryAsync();

            return (OracleValueToInt(rc.Value), OracleValueToInt(days.Value));
        }
        finally
        {
            if (!wasOpen) conn.Close();
        }
    }

    /// <summary>调用只有 p_Result_Code 一个 OUT 参数的存储过程</summary>
    private async Task<int> CallResultCode(string procedure, params OracleParameter[] parameters)
    {
        var conn = _context.Database.GetDbConnection();
        var wasOpen = conn.State == ConnectionState.Open;
        if (!wasOpen) await conn.OpenAsync();

        try
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = procedure;
            cmd.CommandType = CommandType.StoredProcedure;
            cmd.Parameters.AddRange(parameters);
            var rc = new OracleParameter("p_Result_Code", OracleDbType.Int32, ParameterDirection.Output);
            cmd.Parameters.Add(rc);

            await cmd.ExecuteNonQueryAsync();

            return OracleValueToInt(rc.Value);
        }
        finally
        {
            if (!wasOpen) conn.Close();
        }
    }

    /// <summary>OracleDecimal.Value → int 安全转换</summary>
    private static int OracleValueToInt(object? value)
    {
        return value is Oracle.ManagedDataAccess.Types.OracleDecimal od ? (int)od.Value : 0;
    }
}
