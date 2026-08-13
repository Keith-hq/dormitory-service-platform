using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Oracle.ManagedDataAccess.Client;
using Oracle.ManagedDataAccess.Types;
using System.Data;
using TemplateDormApi.Data;
using TemplateDormApi.DTO;
using TemplateDormApi.Exceptions;

namespace TemplateDormApi.Services;

/// <summary>
/// SLA 派单服务实现（难点⑤ 三审修复版）。
/// 派单/接单/完工均走存储过程，C# 侧做薄封装；
/// SLA 升级由 SP 原子标记并返回升级清单，通知经公共服务 INotificationService 投递（数据拥有者边界）。
/// </summary>
public class SlaDispatchService : ISlaDispatchService
{
    private readonly AppDbContext _context;
    private readonly INotificationService _notificationService;
    private readonly ILogger<SlaDispatchService> _logger;

    public SlaDispatchService(
        AppDbContext context,
        INotificationService notificationService,
        ILogger<SlaDispatchService> logger)
    {
        _context = context;
        _notificationService = notificationService;
        _logger = logger;
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

    public async Task<int> CompleteRepair(int ticketId, string adminId, string content, string? repairResult, DateTime? solveTime)
    {
        return await CallResultCode("SP_Complete_Repair",
            new OracleParameter("p_Ticket_ID", ticketId),
            new OracleParameter("p_Admin_ID", adminId),
            new OracleParameter("p_Process_Desc", content),
            new OracleParameter("p_Repair_Result",
                (object?)repairResult ?? DBNull.Value),
            new OracleParameter("p_Solve_Time",
                (object?)solveTime ?? DBNull.Value));
    }

    public async Task EscalateSla()
    {
        var conn = _context.Database.GetDbConnection();
        var wasOpen = conn.State == ConnectionState.Open;
        if (!wasOpen) await conn.OpenAsync();

        try
        {
            // 1. SP 原子标记升级：条件 UPDATE（Escalation_Time IS NULL）+ SQL%ROWCOUNT 守门，
            //    多实例并发巡检时每个工单只会被一个会话赢得；SP 返回该会话本次新升级的工单清单。
            List<(int TicketId, int RoomId, string? AssignedTo, string? MgrAdminId)> escalated;
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "SP_Escalate_SLA";
                cmd.CommandType = CommandType.StoredProcedure;
                var cursor = new OracleParameter("p_Cursor", OracleDbType.RefCursor, ParameterDirection.Output);
                cmd.Parameters.Add(cursor);
                await cmd.ExecuteNonQueryAsync();

                escalated = new List<(int, int, string?, string?)>();
                using var reader = ((OracleRefCursor)cursor.Value).GetDataReader();
                while (await reader.ReadAsync())
                {
                    escalated.Add((
                        reader.GetInt32(0),
                        reader.GetInt32(1),
                        reader.IsDBNull(2) ? null : reader.GetString(2),
                        reader.IsDBNull(3) ? null : reader.GetString(3)));
                }
            }

            // 2. 通知走公共服务（INotificationService 负责收件人解析与通知主键，遵守数据拥有者边界）
            foreach (var row in escalated)
            {
                if (string.IsNullOrWhiteSpace(row.MgrAdminId))
                {
                    continue; // 该楼无楼长 → 跳过
                }

                try
                {
                    await _notificationService.CreateAsync(new NotificationCreateDto
                    {
                        AdminId = row.MgrAdminId,
                        Title = "报修工单 SLA 超时提醒",
                        Content = $"工单#{row.TicketId}（Room_ID={row.RoomId}）已超过处理时限"
                                  + $"（普通/{DateTime.Now:yyyy-MM-dd HH:mm}），请跟进处理。"
                                  + $"当前负责人：{row.AssignedTo ?? "未指派"}",
                        NotificationType = "报修"
                    });
                }
                catch (BusinessException)
                {
                    // 楼长无有效账户等业务性失败 → 跳过该条，不阻断巡检
                }
                catch (Exception ex)
                {
                    // 升级标记已由 SP 原子提交，此时让端点 500 只会让调度方重试，
                    // 而重试再也扫不到该工单（Escalation_Time 已非空）→ 提醒永久丢失。
                    // 因此投递失败记为错误日志，端点仍返回本次升级清单。
                    _logger.LogError(ex,
                        "SLA 升级通知投递失败：工单#{TicketId}（Room_ID={RoomId}，楼长={MgrAdminId}）。"
                        + "升级标记已提交，通知需人工或通知模块重试机制补发。",
                        row.TicketId, row.RoomId, row.MgrAdminId);
                }
            }
        }
        finally
        {
            if (!wasOpen) conn.Close();
        }
    }

    // ===== 查询 =====

    public async Task<PagedResult<PendingRepairTicketDto>> GetPendingTickets(string adminId, int page, int pageSize)
    {
        // 列别名带引号与 DTO 属性名精确一致（Oracle 提供程序列匹配区分大小写）；
        // 分页直接下推 OFFSET/FETCH，不做 EF Skip/Take 二次组合（避免外层引用模型列名 → ORA-00904）。
        var sql = @"SELECT Ticket_ID AS ""TicketId"", Student_ID AS ""StudentId"",
                        Room_ID AS ""RoomId"", Issue_Desc AS ""IssueDesc"",
                        Submit_Time AS ""SubmitTime"", Status AS ""Status"",
                        SLA_Level AS ""SlaLevel"", Deadline AS ""Deadline"",
                        Assigned_To AS ""AssignedTo"", Escalation_Time AS ""EscalationTime""
                 FROM D_Repair_Ticket
                 WHERE Assigned_To = {0} AND Status IN ('待处理', '处理中')
                 ORDER BY
                     CASE SLA_Level WHEN '紧急' THEN 0 ELSE 1 END,
                     Deadline ASC
                 OFFSET {1} ROWS FETCH NEXT {2} ROWS ONLY";

        // 标量查询的输出列必须别名为 "Value"（Oracle 提供程序要求）
        var countSql = @"SELECT COUNT(*) AS ""Value""
                 FROM D_Repair_Ticket
                 WHERE Assigned_To = {0} AND Status IN ('待处理', '处理中')";

        var total = await _context.Database
            .SqlQueryRaw<int>(countSql, adminId)
            .FirstOrDefaultAsync();

        var items = await _context.Database
            .SqlQueryRaw<PendingRepairTicketDto>(sql, adminId, (page - 1) * pageSize, pageSize)
            .ToListAsync();

        return new PagedResult<PendingRepairTicketDto>
        {
            Items = items,
            Total = total,
            Page = page,
            PageSize = pageSize
        };
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
        return value is OracleDecimal od ? (int)od.Value : 0;
    }
}
