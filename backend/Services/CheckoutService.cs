using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TemplateDormApi.Data;
using TemplateDormApi.DTO;
using TemplateDormApi.Exceptions;
using TemplateDormApi.Models;
using TemplateDormApi.Repository;

namespace TemplateDormApi.Services;

/// <summary>
/// 退宿清算服务（DORM-11 登记 / DORM-35 查询 / DORM-36 三步校验 / DORM-37 确认 / DORM-38 取消）。
/// 状态机：待清算 →（校验失败）已拒绝 /（确认）已通过 /（取消）已取消。
/// 三步校验数据源（难点⑥）：D_Fee_Detail（李昂）/ D_Parcel_Record（快递）/ D_Item_Loan（共享物品）
/// 均为只读，不写他人表；结算金额唯一来源为 SP_Calc_Checkout_Fee（IFeeSharingService 调用）。
/// </summary>
public class CheckoutService : ICheckoutService
{
    private const string UkActiveConstraint = "UK_D_CHECKOUT_ACTIVE";

    private readonly AppDbContext _context;
    private readonly CheckoutRepository _checkoutRepo;
    private readonly IFeeSharingService _feeSharing;
    private readonly INotificationService _notificationService;
    private readonly ILogger<CheckoutService> _logger;

    public CheckoutService(
        AppDbContext context,
        CheckoutRepository checkoutRepo,
        IFeeSharingService feeSharing,
        INotificationService notificationService,
        ILogger<CheckoutService> logger)
    {
        _context = context;
        _checkoutRepo = checkoutRepo;
        _feeSharing = feeSharing;
        _notificationService = notificationService;
        _logger = logger;
    }

    public async Task<object> RegisterAsync(long allocationId, CheckoutRegisterDto dto, int? accountId, bool isDormAdmin)
    {
        var alloc = await _context.BedAllocations.FindAsync(allocationId)
            ?? throw new BusinessException(404, "住宿分配不存在", 404);

        var (isAdmin, studentId) = await ResolveCallerAsync(accountId, isDormAdmin);
        EnsureOwner(isAdmin, alloc.StudentId, studentId);

        if (alloc.CheckOutDate != null)
            throw new BusinessException(400, "该住宿分配已退宿");

        if (await _checkoutRepo.GetActiveByAllocationAsync(allocationId) != null)
            throw new BusinessException(409, "已有进行中的退宿申请", 409);

        // dto.Reason / dto.CheckoutDate：D_Checkout_Log 无对应列（DDL 冻结不改）。接口继续兼容接收；
        // 已向 PM 提契约修订（8/14 联调裁决）：checkoutDate 由 DORM-37 confirm 写入 D_Bed_Allocation 覆盖，
        // reason 无对应存储位且无业务价值，建议从契约删除。
        CheckoutLog log;
        try
        {
            log = await _checkoutRepo.AddAsync(new CheckoutLog
            {
                // 主键由序列 SEQ_D_CHECKOUT_LOG_ID + 触发器生成（迁移 023，WHEN NEW IS NULL），不再 MAX+1
                AllocationId = allocationId,
                RequestTime = DateTime.Now,
                Status = CheckoutStatuses.Pending
            });
        }
        catch (DbUpdateException ex)
            when (OracleConstraintParser.TryGetUniqueConstraintName(ex) == UkActiveConstraint)
        {
            // 并发重复登记：数据库唯一索引兜底
            throw new BusinessException(409, "已有进行中的退宿申请", 409);
        }

        return new { checkoutId = log.LogId, allocationId, status = log.Status };
    }

    public async Task<object> GetAsync(int checkoutId, int? accountId, bool isDormAdmin)
    {
        var log = await _checkoutRepo.GetByIdAsync(checkoutId)
            ?? throw new BusinessException(404, "清算记录不存在", 404);

        var alloc = await _context.BedAllocations.FindAsync(log.AllocationId)
            ?? throw new BusinessException(404, "住宿分配不存在", 404);
        var (isAdmin, studentId) = await ResolveCallerAsync(accountId, isDormAdmin);
        EnsureOwner(isAdmin, alloc.StudentId, studentId);

        return await BuildSummaryAsync(log);
    }

    public async Task<object> SettleAsync(int checkoutId, int? accountId, bool isDormAdmin)
    {
        var log = await _checkoutRepo.GetByIdAsync(checkoutId)
            ?? throw new BusinessException(404, "清算记录不存在", 404);

        var alloc = await _context.BedAllocations.FindAsync(log.AllocationId)
            ?? throw new BusinessException(404, "住宿分配不存在", 404);
        var (isAdmin, studentId) = await ResolveCallerAsync(accountId, isDormAdmin);
        EnsureOwner(isAdmin, alloc.StudentId, studentId);

        if (log.Status == CheckoutStatuses.Confirmed)
            throw new BusinessException(400, "退宿已确认，无需重复清算");
        if (log.Status != CheckoutStatuses.Pending)
            throw new BusinessException(400, $"当前状态不可清算（{log.Status}）");

        // 幂等：校验已通过过的清算单重复 settle 直接返回，不重复校验、不重复调 calc
        if (log.FeeCheck == "通过" && log.ItemCheck == "通过")
            return new { checkoutId = log.LogId, status = log.Status, feeCheck = log.FeeCheck, itemCheck = log.ItemCheck, message = "三步校验已通过（重复清算幂等跳过），等待确认退宿" };

        if (string.IsNullOrWhiteSpace(alloc.StudentId))
            throw new BusinessException(400, "住宿分配缺少学号，无法清算");

        // ===== 三步校验（难点⑥：任一步失败即拒绝；数据源只读，跨模块不写入）=====
        // Oracle 兼容（评审整改，8411155 同源）：顶层 AnyAsync 被翻译为
        // CASE WHEN EXISTS(...) THEN True ELSE False，Oracle 21c 无布尔字面量 → ORA-00904。
        // 一律用 CountAsync(...) > 0（翻译为 COUNT(*)）。
        // ① 水电费缴清：该生欠费明细 = 0。注意排除 Bill_Type='退宿' 账单——
        //    退宿结算单由 calc 生成且待缴，属于结算本身而非欠费阻断项。
        var feeOk = await _context.FeeDetails.CountAsync(f =>
            f.StudentId == alloc.StudentId && f.IsPaid == "否" && f.BillType != "退宿") == 0;

        // ② 快递全取走（Pickup_Time 非空）
        var parcelOk = await _context.ParcelRecords.CountAsync(p =>
            p.StudentId == alloc.StudentId && p.PickupTime == null) == 0;

        // ③ 共享物品全归还（Return_Time 非空）
        var itemOk = await _context.ItemLoans.CountAsync(l =>
            l.StudentId == alloc.StudentId && l.ReturnTime == null) == 0;

        log.FeeCheck = feeOk ? "通过" : "未通过";
        log.ItemCheck = parcelOk && itemOk ? "通过" : "未通过";

        var failures = new List<string>();
        if (!feeOk) failures.Add("水电费未缴清");
        if (!parcelOk) failures.Add("存在未取快递");
        if (!itemOk) failures.Add("存在未归还共享物品");

        if (failures.Count > 0)
        {
            // 未通过：状态→已拒绝，原因逐项列出（IT-C2-003/004）
            log.Status = CheckoutStatuses.Rejected;
            log.RejectReason = string.Join("；", failures);
            log.ResultTime = DateTime.Now;
            await _context.SaveChangesAsync();

            // 响应引导（与李昂对齐）：欠费学生清偿路径 = 钱包充值 + STU-05 人工缴费
            // （自动扣款每月 1-3 号只跑当月，历史欠费无自动重扣）；
            // 未通过项处理完后重新发起退宿登记（旧单留档）。
            var guidance = !feeOk
                ? "；欠费请充值钱包后通过学生端账单缴费（STU-05）结清"
                : string.Empty;
            throw new BusinessException(400, $"清算未通过：{log.RejectReason}。处理未通过项后重新发起退宿登记{guidance}");
        }

        // ===== 校验通过：先写 CheckOut_Date，再调 calc（v0.4.1 对齐），同一事务 =====
        // 内存库（单测）不支持事务，按无事务直写；Oracle 走显式事务保证原子性。
        var tx = _context.Database.IsRelational()
            ? await _context.Database.BeginTransactionAsync()
            : null;
        try
        {
            if (alloc.CheckOutDate == null)
                alloc.CheckOutDate = DateTime.Now; // SP 按实际退宿日期折算（v1.1）
            await _context.SaveChangesAsync();

            // SP_Calc_Checkout_Fee（李昂，金额唯一来源）。v1.3 已按分工对齐：过程内不
            // COMMIT、由本事务统一提交（床位/房间/费用原子）；两入口防重由迁移 022
            // 收紧后的 UK_D_FEE_DETAIL (Fee_ID, Student_ID) 唯一性兜底（DUP_VAL_ON_INDEX 跳过）。
            await _feeSharing.CalcCheckoutFee(alloc.StudentId, (int)log.AllocationId);

            if (tx != null) await tx.CommitAsync();
        }
        catch
        {
            if (tx != null) await tx.RollbackAsync();
            throw;
        }
        finally
        {
            if (tx != null) await tx.DisposeAsync();
        }

        return new
        {
            checkoutId = log.LogId,
            status = log.Status,
            feeCheck = log.FeeCheck,
            itemCheck = log.ItemCheck,
            message = "三步校验通过，退宿结算已生成，等待确认退宿"
        };
    }

    public async Task<object> ConfirmAsync(int checkoutId, CheckoutConfirmDto dto, int? accountId, bool isDormAdmin)
    {
        var log = await _checkoutRepo.GetByIdAsync(checkoutId)
            ?? throw new BusinessException(404, "清算记录不存在", 404);

        var alloc = await _context.BedAllocations.FindAsync(log.AllocationId)
            ?? throw new BusinessException(404, "住宿分配不存在", 404);
        var (isAdmin, studentId) = await ResolveCallerAsync(accountId, isDormAdmin);
        EnsureOwner(isAdmin, alloc.StudentId, studentId);

        // 幂等：重复确认不报错、不重复释放床位（IT-C2-001 ③）
        if (log.Status == CheckoutStatuses.Confirmed)
            return await BuildSummaryAsync(log);

        if (log.Status != CheckoutStatuses.Pending)
            throw new BusinessException(400, $"当前状态不可确认退宿（{log.Status}）");

        if (log.FeeCheck != "通过" || log.ItemCheck != "通过")
            throw new BusinessException(400, "三步校验未通过或未执行，请先完成清算（settle）");

        if (!alloc.RoomId.HasValue)
            throw new BusinessException(400, "住宿分配缺少房间信息");

        // Room 主键为 int，Find 需精确类型（BedAllocation.RoomId 为 long）
        var room = await _context.Rooms.FindAsync((int)alloc.RoomId.Value)
            ?? throw new BusinessException(404, "房间不存在", 404);

        log.Status = CheckoutStatuses.Confirmed; // Status 并发令牌：并发 confirm 仅一个生效
        log.ResultTime = DateTime.Now;
        if (alloc.CheckOutDate == null)
            alloc.CheckOutDate = dto.CheckoutDate ?? DateTime.Now; // settle 已写入则保持（幂等）
        room.Occupancy = Math.Max(0, (room.Occupancy ?? 1) - 1);   // 释放床位

        var transitioned = false; // 仅本次请求完成状态迁移才发清算通知（幂等重放/并发让位不发）
        try
        {
            await _context.SaveChangesAsync();
            transitioned = true;
        }
        catch (DbUpdateConcurrencyException)
        {
            // 并发 confirm：另一个请求已确认 → 重读后按幂等返回
            _context.ChangeTracker.Clear();
            log = await _checkoutRepo.GetByIdAsync(checkoutId)
                ?? throw new BusinessException(404, "清算记录不存在", 404);
            if (log.Status != CheckoutStatuses.Confirmed)
                throw new BusinessException(409, "并发确认冲突，请重试", 409);
        }

        if (transitioned && !string.IsNullOrWhiteSpace(alloc.StudentId))
            await NotifyCheckoutCompletedAsync(alloc.StudentId, log.LogId);

        return await BuildSummaryAsync(log);
    }

    public async Task<object> CancelAsync(int checkoutId, int? accountId, bool isDormAdmin)
    {
        var log = await _checkoutRepo.GetByIdAsync(checkoutId)
            ?? throw new BusinessException(404, "清算记录不存在", 404);

        var alloc = await _context.BedAllocations.FindAsync(log.AllocationId)
            ?? throw new BusinessException(404, "住宿分配不存在", 404);
        var (isAdmin, studentId) = await ResolveCallerAsync(accountId, isDormAdmin);
        EnsureOwner(isAdmin, alloc.StudentId, studentId);

        if (log.Status == CheckoutStatuses.Cancelled)
            return await BuildSummaryAsync(log); // 幂等

        if (log.Status == CheckoutStatuses.Confirmed)
            throw new BusinessException(400, "退宿已确认，不可取消");

        if (log.Status != CheckoutStatuses.Pending)
            throw new BusinessException(400, $"当前状态不可取消（{log.Status}）");

        log.Status = CheckoutStatuses.Cancelled;
        log.ResultTime = DateTime.Now;
        if (alloc.CheckOutDate != null)
            alloc.CheckOutDate = null; // 回滚 settle 写入的退宿日期，床位恢复在住（IT-C2-005 ②）

        // 审计留痕（IT-C2-005 ③）：D_Audit_Event 属审计域（FP5-4 徐亦尘），跨模块写入必须走
        // 其对外接口/公共服务（架构红线）。复审要求：服务未合入前不得直接落表，先移除。
        // TODO(审计模块接口就绪后): 调用审计公共服务记录"退宿清算取消"（D_CHECKOUT_LOG / LogId）

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            // 并发 cancel：另一个请求已处理 → 重读后按幂等/状态判定
            _context.ChangeTracker.Clear();
            log = await _checkoutRepo.GetByIdAsync(checkoutId)
                ?? throw new BusinessException(404, "清算记录不存在", 404);
            if (log.Status == CheckoutStatuses.Confirmed)
                throw new BusinessException(400, "退宿已确认，不可取消");
            if (log.Status != CheckoutStatuses.Cancelled)
                throw new BusinessException(409, "并发取消冲突，请重试", 409);
        }

        return await BuildSummaryAsync(log);
    }

    /// <summary>
    /// 清算通知（IT-C2-001 ⑥）：confirm 状态迁移成功后经公共服务 INotificationService 投递，
    /// 通知主键与收件人解析归通知域（数据拥有者边界，与 SlaDispatchService 同模式）。
    /// 通知失败不阻断退宿确认；非业务性失败记错误日志（退宿状态已提交，接口若 500，
    /// 重试因幂等直接返回，通知将永久丢失——与 SlaDispatchService 升级通知同口径）。
    /// </summary>
    private async Task NotifyCheckoutCompletedAsync(string studentId, int checkoutId)
    {
        try
        {
            await _notificationService.CreateAsync(new NotificationCreateDto
            {
                StudentId = studentId,
                Title = "退宿办理完成",
                Content = $"退宿清算单#{checkoutId}已确认通过，床位已释放。如有退宿账单请及时缴清。",
                NotificationType = "系统"
            });
        }
        catch (BusinessException)
        {
            // 收件人无有效账户等业务性失败 → 跳过，不阻断退宿确认
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "退宿完成通知投递失败：清算单#{CheckoutId}（学生={StudentId}）。退宿状态已提交，通知需人工或通知模块重试机制补发。",
                checkoutId, studentId);
        }
    }

    /// <summary>
    /// 解析调用者身份（鉴权归属，评审整改）：DormAdmin 角色放行（宿管代办，无学生身份）；
    /// 其余按 JWT 账户解析学生身份（WalletController.ResolveStudentId 同范式）。
    /// </summary>
    private async Task<(bool IsAdmin, string StudentId)> ResolveCallerAsync(int? accountId, bool isDormAdmin)
    {
        if (isDormAdmin) return (true, string.Empty);

        if (!accountId.HasValue)
            throw new BusinessException(401, "未登录或 Token 无效");

        var studentId = await _context.UserAccounts
            .Where(a => a.AccountId == accountId.Value)
            .Select(a => a.StudentId)
            .FirstOrDefaultAsync();
        if (string.IsNullOrWhiteSpace(studentId))
            throw new BusinessException(401, "当前账户未关联学生身份");

        return (false, studentId);
    }

    /// <summary>归属校验：学生仅能操作本人分配对应的清算单，宿管放行</summary>
    private static void EnsureOwner(bool isAdmin, string? allocStudentId, string callerStudentId)
    {
        if (!isAdmin && !string.Equals(allocStudentId, callerStudentId, StringComparison.Ordinal))
            throw new BusinessException(403, "无权操作他人的退宿申请", 403);
    }

    /// <summary>DORM-35 响应：清算记录 + 床位分配快照</summary>
    private async Task<object> BuildSummaryAsync(CheckoutLog log)
    {
        var alloc = await _context.BedAllocations.FindAsync(log.AllocationId);
        return new
        {
            checkoutId = log.LogId,
            allocationId = log.AllocationId,
            // 响应层口径：DB CHECK 约束仅允许"已通过"，对外契约（IT-C2-001 ④）要求终态"已清算"，
            // 查询响应统一映射；落库值不变（8/14 联调与前端确认取数口径）。
            status = log.Status == CheckoutStatuses.Confirmed ? "已清算" : log.Status,
            feeCheck = log.FeeCheck,
            itemCheck = log.ItemCheck,
            rejectReason = log.RejectReason,
            requestTime = log.RequestTime,
            resultTime = log.ResultTime,
            allocation = alloc == null
                ? null
                : new
                {
                    studentId = alloc.StudentId,
                    roomId = alloc.RoomId,
                    bedNo = alloc.BedNo,
                    checkInDate = alloc.CheckInDate,
                    checkOutDate = alloc.CheckOutDate
                }
        };
    }
}
