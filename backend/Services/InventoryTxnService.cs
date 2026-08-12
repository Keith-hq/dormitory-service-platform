using Microsoft.EntityFrameworkCore;
using Oracle.ManagedDataAccess.Client;
using System.Data;
using TemplateDormApi.Data;
using TemplateDormApi.Models;

namespace TemplateDormApi.Services;

/// <summary>
/// 共享物品借还与耗材出库服务实现（难点④）。
/// 所有写操作委托给 PL/SQL 存储过程，C# 侧只做参数绑定的薄封装。
/// </summary>
public class InventoryTxnService : IInventoryTxnService
{
    private readonly AppDbContext _context;

    public InventoryTxnService(AppDbContext context)
    {
        _context = context;
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
        return await CallResultCode("SP_Return_Item",
            new OracleParameter("p_Loan_ID", loanId),
            new OracleParameter("p_Student_ID", studentId));
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

    public async Task<List<ItemLoan>> GetItemLoans(string studentId)
    {
        return await _context.Set<ItemLoan>()
            .FromSqlRaw(
                @"SELECT Loan_ID AS LoanId, Item_ID AS ItemId, Student_ID AS StudentId,
                        Borrow_Time AS BorrowTime, Due_Time AS DueTime, Return_Time AS ReturnTime
                 FROM D_Item_Loan
                 WHERE Student_ID = {0}
                 ORDER BY Borrow_Time DESC", studentId)
            .ToListAsync();
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
