using Microsoft.EntityFrameworkCore;
using Oracle.ManagedDataAccess.Client;
using System.Data;
using System.Data.Common;
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
/// 逾期巡检（CheckOverdue）：提醒走通知公共服务（借出行锁互斥 + 同事务检查插入），
/// 并对"已归还但扣分待补偿"的借出做自愈补扣，信用服务临时失败不阻塞归还结果。
/// </summary>
public class InventoryTxnService : IInventoryTxnService
{
    private readonly AppDbContext _context;
    private readonly ICreditService _creditService;
    private readonly INotificationService _notificationService;
    private readonly ILogger<InventoryTxnService> _logger;

    private const string OverdueReminderTitle = "共享物品逾期归还提醒";

    public InventoryTxnService(
        AppDbContext context,
        ICreditService creditService,
        INotificationService notificationService,
        ILogger<InventoryTxnService> logger)
    {
        _context = context;
        _creditService = creditService;
        _notificationService = notificationService;
        _logger = logger;
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

    /// <summary>
    /// 归还共享物品。返回 (ResultCode, CreditPending)：
    /// CreditPending=true 表示归还已完成但超期扣分未完成（信用服务临时失败），
    /// 系统巡检会自愈补扣——接口不能把"已归还但扣分待补偿"伪装成归还失败。
    /// </summary>
    public async Task<(int ResultCode, bool CreditPending)> ReturnItem(int loanId, string studentId)
    {
        var (rc, overdueDays, isOverdue) = await CallReturnSp(loanId, studentId);

        if (rc == 0)
        {
            // 四审 P1-1：是否逾期以 Return_Time > Due_Time 为准（SP p_Is_Overdue），
            // overdueDays 仅用于展示（至少 1 天），刚超时 1 秒/1 小时同样触发扣分
            if (isOverdue == 1)
            {
                return (0, !await TryDeductOverdueAsync(loanId, studentId, overdueDays));
            }
            return (0, false);
        }

        // rc=1（不存在/已归还/非本人）补偿路径：SP 成功但应用在扣分前崩溃时，
        // 客户端重试归还得到 rc=1；若记录确属本人、已归还且逾期，补上未完成的扣分。
        // Event_Key 幂等保证最多扣一次，跨用户重试则不会命中本人条件。
        var loan = await _context.Set<ItemLoan>().AsNoTracking()
            .FirstOrDefaultAsync(l => l.LoanId == loanId && l.StudentId == studentId);
        if (loan is { ReturnTime: not null } && loan.ReturnTime > loan.DueTime)
        {
            var days = Math.Max(1, (int)(loan.ReturnTime.Value.Date - loan.DueTime.Date).TotalDays);
            return (1, !await TryDeductOverdueAsync(loanId, studentId, days));
        }
        return (1, false);
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

    /// <summary>
    /// 逾期巡检（Quartz 每 15 分钟）：
    /// 1. 提醒——读取逾期未归还候选，经通知公共服务投递系统通知；
    /// 2. 自愈补扣——对"已归还且逾期但无 OVERDUE-{Loan_ID} 信用流水"的借出
    ///    （归还时信用服务临时失败遗留的待补偿项）重试扣分。
    /// 通知/补扣失败只记录，不回滚主业务。
    /// </summary>
    public async Task CheckOverdue()
    {
        await SendOverdueRemindersAsync();
        await CompensatePendingDeductionsAsync();
    }

    /// <summary>
    /// 逾期提醒（四审 P1-3）：不再经 SP 直写 D_Notification / D_User_Account，
    /// 全部经通知公共服务。幂等语义：以借出记录行锁（FOR UPDATE）为互斥原语，
    /// 检查+插入在同一事务内完成——并发巡检（多实例/内部端点）在同一笔借出上
    /// 串行化，同一天每笔只提醒一次（标题 + 内容内嵌 Loan_ID 去重）。
    /// </summary>
    private async Task SendOverdueRemindersAsync()
    {
        var today = DateTime.Today;
        var overdueLoans = await (from l in _context.ItemLoans
                                  join ua in _context.UserAccounts on l.StudentId equals ua.StudentId
                                  where l.ReturnTime == null
                                        && l.DueTime < DateTime.Now
                                        && ua.AccountStatus == "正常"
                                  select new { l.LoanId, l.StudentId, l.ItemId, l.DueTime, ua.AccountId })
            .AsNoTracking()
            .ToListAsync();

        foreach (var loan in overdueLoans)
        {
            try
            {
                await using var tx = await _context.Database.BeginTransactionAsync();

                // 行锁互斥：并发巡检在此串行化，锁内检查+插入保证同日仅一条提醒
                await _context.Database.ExecuteSqlRawAsync(
                    "SELECT Loan_ID FROM D_Item_Loan WHERE Loan_ID = {0} FOR UPDATE",
                    loan.LoanId);

                // 四审真实 Oracle 实测：AnyAsync 被 provider 翻译为
                // CASE WHEN EXISTS(...) THEN True ELSE False，Oracle 21c 无
                // 布尔字面量 → ORA-00904。改用 CountAsync（翻译为 COUNT(*)）。
                var already = await _context.Notifications.CountAsync(n =>
                    n.RecipientAccountId == loan.AccountId
                    && n.Title == OverdueReminderTitle
                    && n.Content.Contains($"Loan_ID={loan.LoanId}，")
                    && n.CreateTime >= today) > 0;

                if (!already)
                {
                    var days = Math.Max(1, (int)(DateTime.Today - loan.DueTime.Date).TotalDays);
                    await _notificationService.CreateAsync(new NotificationCreateDto
                    {
                        StudentId = loan.StudentId,
                        Title = OverdueReminderTitle,
                        Content = $"您借用的共享物品（Loan_ID={loan.LoanId}，Item_ID={loan.ItemId}）已逾期 {days} 天未归还。请尽快归还；超期归还将按次扣除 2 分信用分。",
                        NotificationType = "系统"
                    });
                }

                await tx.CommitAsync();
            }
            catch (Exception ex) when (ex is BusinessException || ex is DbException)
            {
                // 六审 P3-2 收窄：只吞业务/数据库类可重试失败——单个候选失败不影响
                // 其余候选与主业务；编程错误向上抛出令巡检 Job 失败（与补扣路径
                // catch 收窄策略一致，避免"捕获一切"掩盖缺陷）。
                // 五审建议：投递失败后通知实体仍滞留 DbContext Added 状态，
                // 同上下文后续 SaveChanges 会重放失败插入——此处 Detach 清除跟踪，
                // 失败留在本笔（事务已随 using 回滚），不污染后续业务。
                foreach (var entry in _context.ChangeTracker.Entries<Notification>()
                             .Where(e => e.State == EntityState.Added).ToList())
                {
                    entry.State = EntityState.Detached;
                }

                _logger.LogError(ex, "逾期提醒投递失败 Loan_ID={LoanId}", loan.LoanId);
            }
        }
    }

    /// <summary>
    /// 自愈补扣（四审 P1-2）：归还已提交但信用扣分失败（信用服务临时异常）时，
    /// 由巡检扫描"近 7 天已归还逾期、无 OVERDUE-{Loan_ID} 流水"的借出并重试扣分。
    /// Event_Key 幂等保证已扣分的不重复。
    /// 时间窗收窄（五审建议）：只补偿近 7 天归还的借出，更早的补偿窗口视为关闭
    /// （每 15 分钟全表扫描无限重扫的问题就此收敛）；永久性跳过（40401/分数低于
    /// 罚分）由 DeductOverdueAsync 内部关闭，不再每轮进入。
    /// </summary>
    private async Task CompensatePendingDeductionsAsync()
    {
        var windowStart = DateTime.Now.AddDays(-7);
        var returnedOverdue = await _context.ItemLoans
            .Where(l => l.ReturnTime != null && l.ReturnTime > l.DueTime
                        && l.ReturnTime >= windowStart)
            .AsNoTracking()
            .ToListAsync();

        foreach (var loan in returnedOverdue)
        {
            var eventKey = $"OVERDUE-{loan.LoanId}";
            // 四审真实 Oracle 实测：AnyAsync 翻译含 True/False 布尔字面量，
            // Oracle 21c 不支持 → ORA-00904。改用 CountAsync（翻译为 COUNT(*)）。
            if (await _context.CreditLogs.AsNoTracking().CountAsync(c => c.EventKey == eventKey) > 0)
            {
                continue; // 已扣分，跳过
            }

            var days = Math.Max(1, (int)(loan.ReturnTime!.Value.Date - loan.DueTime.Date).TotalDays);
            try
            {
                await DeductOverdueAsync(loan.LoanId, loan.StudentId, days);
                _logger.LogInformation("逾期补扣完成 Loan_ID={LoanId}", loan.LoanId);
            }
            catch (Exception ex) when (ex is BusinessException || ex is DbException)
            {
                // 可重试失败只记录：下一轮巡检继续重试；编程错误向上抛出
                _logger.LogWarning(ex, "逾期补扣失败 Loan_ID={LoanId}，待下轮巡检重试", loan.LoanId);
            }
        }
    }

    // ===== 查询 =====

    public async Task<List<SharedItem>> GetSharedItems(int? buildingId = null)
    {
        // 别名必须带引号：Oracle 把裸别名大写（ItemId → ITEMID），与 EF 列映射
        // （ITEM_ID 等）不一致 → "The required column 'ITEM_ID' was not present"。
        // 与 GetItemLoans 同模式：带引号别名固定列名大小写。
        if (buildingId.HasValue)
        {
            return await _context.Set<SharedItem>()
                .FromSqlRaw(
                    @"SELECT Item_ID AS ""ItemId"", Item_Name AS ""ItemName"",
                            Building_ID AS ""BuildingId"", Total_Qty AS ""TotalQty"",
                            Available_Qty AS ""AvailableQty"", Status
                     FROM D_Shared_Item
                     WHERE Status = '正常' AND Available_Qty > 0 AND Building_ID = {0}
                     ORDER BY Item_ID", buildingId.Value)
                .ToListAsync();
        }

        return await _context.Set<SharedItem>()
            .FromSqlRaw(
                @"SELECT Item_ID AS ""ItemId"", Item_Name AS ""ItemName"",
                        Building_ID AS ""BuildingId"", Total_Qty AS ""TotalQty"",
                        Available_Qty AS ""AvailableQty"", Status
                 FROM D_Shared_Item
                 WHERE Status = '正常' AND Available_Qty > 0
                 ORDER BY Item_ID")
            .ToListAsync();
    }

    public async Task<PagedResult<ItemLoan>> GetItemLoans(string studentId, int page, int pageSize)
    {
        // 四审补充验证：EF Core 标量 SQL 查询要求输出列命名为 Value，
        // 显式 AS "Value" 确保 Oracle 提供商的标量列映射稳定
        var total = await _context.Database
            .SqlQueryRaw<int>("SELECT COUNT(*) AS \"Value\" FROM D_Item_Loan WHERE Student_ID = {0}", studentId)
            .FirstOrDefaultAsync();

        // 四审补充验证（真实 Oracle 实测）：
        // 1) FromSqlRaw 上叠加 Skip/Take 会让 EF 把原始 SQL 包成子查询并按模型列名
        //    （"t"."LOAN_ID"）做外层投影，与别名（LoanId→LOANID）不一致 → ORA-00904；
        // 2) 非组合 FromSqlRaw 要求读列名与模型列名完全一致（大小写敏感），
        //    Oracle 提供商会按查询文本原样报告列名。
        // 因此分页下推到原生 SQL（Oracle 12c+ OFFSET/FETCH），
        // 且用带引号别名固定列名为模型列名（"LOAN_ID" 等）。
        var offset = (page - 1) * pageSize;
        var items = await _context.Set<ItemLoan>()
            .FromSqlRaw(@"SELECT Loan_ID AS ""LOAN_ID"", Item_ID AS ""ITEM_ID"",
                                Student_ID AS ""STUDENT_ID"", Borrow_Time AS ""BORROW_TIME"",
                                Due_Time AS ""DUE_TIME"", Return_Time AS ""RETURN_TIME""
                         FROM D_Item_Loan
                         WHERE Student_ID = {0}
                         ORDER BY Borrow_Time DESC
                         OFFSET {1} ROWS FETCH NEXT {2} ROWS ONLY",
                studentId, offset, pageSize)
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
                @"SELECT Material_ID AS ""MaterialId"", Material_Name AS ""MaterialName"",
                        Unit, Stock_Qty AS ""StockQty""
                 FROM D_Repair_Material
                 ORDER BY Material_ID")
            .ToListAsync();
    }

    // ===== 私有辅助 =====

    /// <summary>
    /// 尝试超期扣分，返回是否已闭环（true=已扣分或无需扣分，false=扣分失败待补偿）。
    /// 归还主流程已提交，扣分失败绝不能把归还伪装成失败——调用方凭 false 标记待补偿，
    /// 由巡检自愈重试。
    /// </summary>
    private async Task<bool> TryDeductOverdueAsync(int loanId, string studentId, int overdueDays)
    {
        try
        {
            await DeductOverdueAsync(loanId, studentId, overdueDays);
            return true;
        }
        catch (Exception ex) when (ex is BusinessException || ex is DbException)
        {
            // 只吞业务/数据库类可重试失败：归还已生效，扣分失败标记待补偿交巡检重试；
            // 编程错误（NRE 等）向上抛出让调用链暴露，避免"捕获一切"掩盖缺陷。
            _logger.LogError(ex,
                "超期扣分失败（归还已生效，标记待补偿）Loan_ID={LoanId} Student_ID={StudentId}",
                loanId, studentId);
            return false;
        }
    }

    /// <summary>
    /// 超期归还按次扣 2 分（PRD 规则，组长确认）：统一走信用分公共服务。
    /// Event_Key = OVERDUE-{loanId}：与巡检并发、客户端重试都只产生一次扣分，
    /// 且冻结（跌破 60）通知由信用分服务统一发出，语义不丢失。
    /// FloorAtZero = true（七审恢复，架构师确认）：低分学生（如 1 分）扣 2 分在服务
    /// 锁内封底到 0，不再因"结果低于 0"被拒——「结果超范围」400 从此不可能发生，
    /// 400 只剩可重试语义（系统繁忙/数据冲突/参数校验不可触发），全部交巡检重试。
    /// 永久性跳过仅 40401（账户不存在/停用）；其余异常向上抛出，由
    /// TryDeductOverdueAsync 捕获后标记待补偿、交巡检自愈重试。
    /// </summary>
    private async Task DeductOverdueAsync(int loanId, string studentId, int overdueDays)
    {
        try
        {
            await _creditService.DeductAsync(new CreditDeductDto
            {
                StudentId = studentId,
                ScoreChange = -2,
                Reason = $"共享物品超期归还（Loan_ID={loanId}，逾期{overdueDays}天，按次扣2分）",
                EventKey = $"OVERDUE-{loanId}",
                FloorAtZero = true
            }, CancellationToken.None);
        }
        catch (BusinessException ex) when (ex.Code == 40401)
        {
            return; // 学生账户不存在或已停用，跳过扣分（自愈巡检不再重试）
        }
    }

    /// <summary>调用 SP_Return_Item，返回 (resultCode, overdueDays, isOverdue)</summary>
    private async Task<(int rc, int overdueDays, int isOverdue)> CallReturnSp(int loanId, string studentId)
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
            var isOverdue = new OracleParameter("p_Is_Overdue", OracleDbType.Int32, ParameterDirection.Output);
            cmd.Parameters.Add(rc);
            cmd.Parameters.Add(days);
            cmd.Parameters.Add(isOverdue);

            await cmd.ExecuteNonQueryAsync();

            return (OracleValueToInt(rc.Value), OracleValueToInt(days.Value), OracleValueToInt(isOverdue.Value));
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
