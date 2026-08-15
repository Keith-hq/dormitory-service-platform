using Microsoft.EntityFrameworkCore;
using Oracle.ManagedDataAccess.Client;
using Oracle.ManagedDataAccess.Types;
using System.Data;
using TemplateDormApi.Data;
using TemplateDormApi.DTO;

namespace TemplateDormApi.Services;

/// <summary>
/// 钱包服务实现（难点② 缴费/钱包端点）。
/// 缴费/充值均为单域原子操作：SP 自 COMMIT（同 SP_Auto_Deduct），C# 侧不包事务；
/// 幂等由 SP 内的 Idempotency-Key 重放检查 + UK_D_WALLET_LOG_KEY 线性化兜底保证
/// （详见 database/sp/sp_wallet.sql 文件头语义说明）。
/// </summary>
public class WalletService : IWalletService
{
    private readonly AppDbContext _context;
    private readonly IBillingService _billingService;

    public WalletService(AppDbContext context, IBillingService billingService)
    {
        _context = context;
        _billingService = billingService;
    }

    public Task<int> ManualPay(int detailId, string studentId, string idempotencyKey)
    {
        return CallResultCode("SP_Manual_Pay",
            new OracleParameter("p_Detail_ID", detailId),
            new OracleParameter("p_Student_ID", studentId),
            new OracleParameter("p_Idempotency_Key", idempotencyKey));
    }

    public Task<int> Recharge(string studentId, decimal amount, string idempotencyKey)
    {
        return CallResultCode("SP_Recharge",
            new OracleParameter("p_Student_ID", studentId),
            new OracleParameter("p_Amount", amount),
            new OracleParameter("p_Idempotency_Key", idempotencyKey));
    }

    public async Task<WalletViewDto> GetWallet(string studentId, string yearMonth)
    {
        var balance = await _billingService.GetBalance(studentId);
        var logs = await _billingService.GetWalletLogs(studentId, yearMonth);

        return new WalletViewDto
        {
            StudentId = studentId,
            Balance = balance,
            Logs = logs.Select(l => new WalletLogDto
            {
                LogId = l.LogId,
                Amount = l.Amount,
                TransactionType = l.TransactionType,
                BeforeBalance = l.BeforeBalance,
                AfterBalance = l.AfterBalance,
                DetailId = l.DetailId,
                CreateTime = l.CreateTime
            }).ToList()
        };
    }

    /// <summary>
    /// 通用 SP 结果码调用（照抄 SlaDispatchService.CallResultCode 模式）：
    /// OUT 参数 p_Result_Code 读回业务结果，SP 自身不 RAISE 业务失败。
    /// </summary>
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

    private static int OracleValueToInt(object? value)
    {
        return value is OracleDecimal od ? (int)od.Value : 0;
    }
}
