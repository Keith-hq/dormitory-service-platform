using Microsoft.EntityFrameworkCore;
using TemplateDormApi.Data;
using TemplateDormApi.Models;
using System.Data;
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
        // 2026-08-15 实测修正：不能 using 释放 DbContext 的共享连接
        // （"Cannot access a disposed object"），改为 wasOpen 模式：
        // 借用连接查询，仅当本方法打开时才关闭
        var conn = _context.Database.GetDbConnection();
        var wasOpen = conn.State == ConnectionState.Open;
        if (!wasOpen) await conn.OpenAsync();

        try
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT Balance FROM D_Wallet_Account WHERE Student_ID = :id";
            var param = cmd.CreateParameter();
            param.ParameterName = "id";
            param.Value = studentId;
            cmd.Parameters.Add(param);
            var result = await cmd.ExecuteScalarAsync();
            return result is DBNull or null ? 0 : Convert.ToDecimal(result);
        }
        finally
        {
            if (!wasOpen) conn.Close();
        }
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
        // 与 GetBalance 同款 wasOpen 模式（不释放 DbContext 共享连接）
        var conn = _context.Database.GetDbConnection();
        var wasOpen = conn.State == ConnectionState.Open;
        if (!wasOpen) await conn.OpenAsync();

        try
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT Power_Status FROM D_Room WHERE Room_ID = :id";
            var param = cmd.CreateParameter();
            param.ParameterName = "id";
            param.Value = roomId;
            cmd.Parameters.Add(param);
            var result = await cmd.ExecuteScalarAsync();
            return result is DBNull or null ? "正常" : result.ToString()!;
        }
        finally
        {
            if (!wasOpen) conn.Close();
        }
    }
}
