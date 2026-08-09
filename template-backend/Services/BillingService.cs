using Microsoft.EntityFrameworkCore;
using TemplateDormApi.Data;
using TemplateDormApi.Models;
using System.Data.Common;

namespace TemplateDormApi.Services;

/// <summary>
/// 账单划扣与断电服务：封装难点②的三个存储过程
/// </summary>
public class BillingService : IBillingService
{
    private readonly AppDbContext _context;

    public BillingService(AppDbContext context)
    {
        _context = context;
    }

    public async Task AutoDeduct(int attemptNo, string yearMonth)
    {
        await _context.Database.ExecuteSqlRawAsync(
            "BEGIN SP_Auto_Deduct({0}, {1}); END;",
            attemptNo, yearMonth);
    }

    public async Task CheckPowerCut(string yearMonth)
    {
        await _context.Database.ExecuteSqlRawAsync(
            "BEGIN SP_Check_Power_Cut({0}); END;",
            yearMonth);
    }

    public async Task RestorePower()
    {
        await _context.Database.ExecuteSqlRawAsync(
            "BEGIN SP_Restore_Power; END;");
    }

    public async Task<decimal> GetBalance(string studentId)
    {
        using var conn = _context.Database.GetDbConnection();
        await conn.OpenAsync();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Balance FROM D_Wallet_Account WHERE Student_ID = :id";
        var param = cmd.CreateParameter();
        param.ParameterName = "id";
        param.Value = studentId;
        cmd.Parameters.Add(param);
        var result = await cmd.ExecuteScalarAsync();
        return result is DBNull or null ? 0 : Convert.ToDecimal(result);
    }

    public async Task<List<WalletLog>> GetWalletLogs(string studentId, string yearMonth)
    {
        return await _context.Set<WalletLog>()
            .FromSqlRaw(
                @"SELECT * FROM D_Wallet_Log
                  WHERE Student_ID = {0}
                    AND TO_CHAR(Create_Time, 'YYYY-MM') = {1}
                  ORDER BY Create_Time DESC",
                studentId, yearMonth)
            .ToListAsync();
    }

    public async Task<string> GetPowerStatus(int roomId)
    {
        using var conn = _context.Database.GetDbConnection();
        await conn.OpenAsync();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Power_Status FROM D_Room WHERE Room_ID = :id";
        var param = cmd.CreateParameter();
        param.ParameterName = "id";
        param.Value = roomId;
        cmd.Parameters.Add(param);
        var result = await cmd.ExecuteScalarAsync();
        return result is DBNull or null ? "正常" : result.ToString()!;
    }
}
