using Microsoft.EntityFrameworkCore;
using Oracle.ManagedDataAccess.Client;
using System.Data;
using TemplateDormApi.Data;
using TemplateDormApi.Models;

namespace TemplateDormApi.Services;

/// <summary>
/// SLA 派单服务实现（难点⑤）。
/// 派单/接单/完工均走存储过程，C# 侧做薄封装。
/// </summary>
public class SlaDispatchService : ISlaDispatchService
{
    private readonly AppDbContext _context;

    public SlaDispatchService(AppDbContext context)
    {
        _context = context;
    }

    // ===== 写操作 =====

    public async Task<int> AssignTicket(int ticketId)
    {
        return await CallResultCode("SP_Assign_Ticket",
            new OracleParameter("p_Ticket_ID", ticketId));
    }

    public async Task<int> ClaimTicket(int ticketId, string adminId)
    {
        return await CallResultCode("SP_Claim_Ticket",
            new OracleParameter("p_Ticket_ID", ticketId),
            new OracleParameter("p_Admin_ID", adminId));
    }

    public async Task<int> CompleteRepair(int ticketId, string adminId, string processDesc)
    {
        return await CallResultCode("SP_Complete_Repair",
            new OracleParameter("p_Ticket_ID", ticketId),
            new OracleParameter("p_Admin_ID", adminId),
            new OracleParameter("p_Process_Desc", processDesc));
    }

    public async Task EscalateSla()
    {
        await _context.Database.ExecuteSqlRawAsync("BEGIN SP_Escalate_SLA; END;");
    }

    // ===== 查询 =====

    public async Task<List<RepairTicket>> GetPendingTickets(string adminId)
    {
        return await _context.Set<RepairTicket>()
            .FromSqlRaw(
                @"SELECT Ticket_ID AS TicketId, Student_ID AS StudentId,
                        Room_ID AS RoomId, Issue_Desc AS IssueDesc,
                        Submit_Time AS SubmitTime, Status,
                        SLA_Level AS SlaLevel, Deadline, Assigned_To AS AssignedTo
                 FROM D_Repair_Ticket
                 WHERE Assigned_To = {0} AND Status IN ('待处理', '处理中')
                 ORDER BY
                     CASE SLA_Level WHEN '紧急' THEN 0 ELSE 1 END,
                     Deadline ASC",
                adminId)
            .ToListAsync();
    }

    // ===== 私有辅助 =====

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
        return value is Oracle.ManagedDataAccess.Types.OracleDecimal od ? (int)od.Value : 0;
    }
}
