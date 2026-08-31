using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TemplateDormApi.Data;
using TemplateDormApi.DTO;
using TemplateDormApi.Exceptions;
using TemplateDormApi.Models;
using TemplateDormApi.Repository;

namespace TemplateDormApi.Services;

/// <summary>
/// 信用分申诉服务实现（APPEAL-01/02/03）。
/// 复核通过时经 ICreditService.RestoreAsync 恢复信用分（EventKey=APPEAL-{id} 幂等），
/// 不直接写信用分表（数据拥有者边界：信用账户/流水归信用公共服务）。
/// </summary>
public class CreditAppealService : ICreditAppealService
{
    private const string StatusPending = "待复核";
    private const string StatusApproved = "已通过";
    private const string StatusRejected = "已驳回";

    private readonly AppDbContext _context;
    private readonly UserAccountRepository _userAccountRepository;
    private readonly ICreditService _creditService;
    private readonly INotificationService _notificationService;
    private readonly IAuditService _auditService;
    private readonly ILogger<CreditAppealService> _logger;

    public CreditAppealService(
        AppDbContext context,
        UserAccountRepository userAccountRepository,
        ICreditService creditService,
        INotificationService notificationService,
        IAuditService auditService,
        ILogger<CreditAppealService> logger)
    {
        _context = context;
        _userAccountRepository = userAccountRepository;
        _creditService = creditService;
        _notificationService = notificationService;
        _auditService = auditService;
        _logger = logger;
    }

    public async Task<CreditAppealDto> SubmitAsync(
        int accountId,
        CreateCreditAppealRequest dto,
        CancellationToken cancellationToken)
    {
        var studentId = await ResolveOwnStudentIdAsync(accountId, cancellationToken);

        var log = await _context.CreditLogs.AsNoTracking()
            .FirstOrDefaultAsync(l => l.LogId == dto.CreditRecordId && l.StudentId == studentId, cancellationToken)
            ?? throw new BusinessException(400, "扣分明细不存在或不属于本人");

        // 只能申诉扣分明细：正分流水（如月度重置 +45）被申诉通过会经 Math.Abs 二次加分。
        // 在提交侧拦截，保证被申诉明细恒为负值（ScoreChange < 0）。
        if (log.ScoreChange >= 0)
        {
            throw new BusinessException(400, "只能申诉扣分明细，正分流水不可申诉");
        }

        // 同一扣分只能申诉一次（DB 唯一约束 UK_D_CREDIT_APPEAL_LOG 兜底）。
        // 用 CountAsync 而非 AnyAsync：Oracle provider 会把 AnyAsync 翻译成
        // 布尔字面量 TRUE/FALSE，Oracle 21c 无此语法 → ORA-00904（同 v0.15 已记模式）。
        var exists = await _context.CreditAppeals
            .CountAsync(a => a.CreditLogId == dto.CreditRecordId, cancellationToken) > 0;
        if (exists)
        {
            throw new BusinessException(400, "该扣分明细已申诉过，请勿重复申诉");
        }

        var appeal = new CreditAppeal
        {
            // 041：申诉表不再冗余 StudentId，学生归属唯一载体为被申诉流水
            // D_Credit_Log.Student_ID（提交侧已校验 log.StudentId == 本人）
            CreditLogId = dto.CreditRecordId,
            Reason = dto.Reason,
            Status = StatusPending,
            CreateTime = DateTime.Now
        };
        _context.CreditAppeals.Add(appeal);
        await _context.SaveChangesAsync(cancellationToken);

        return await ToDtoAsync(appeal, cancellationToken);
    }

    public async Task<PagedResult<CreditAppealDto>> GetMyAsync(
        int accountId,
        string studentId,
        int page,
        int pageSize,
        bool isDormAdmin,
        CancellationToken cancellationToken)
    {
        // 鉴权：宿管/超管显式放行；其余账号必须是学生本人（非学生账号不得查任意学生申诉）
        var currentStudentId = await _userAccountRepository.GetStudentIdByAccountIdAsync(
            accountId,
            cancellationToken);
        if (!isDormAdmin)
        {
            if (string.IsNullOrWhiteSpace(currentStudentId))
            {
                throw new BusinessException(403, "仅学生本人或宿管/超管可查看申诉", 403);
            }

            if (!string.Equals(currentStudentId, studentId, StringComparison.Ordinal))
            {
                throw new BusinessException(403, "无权查看他人申诉", 403);
            }
        }

        page = Math.Max(page, 1);
        pageSize = pageSize is < 1 or > 100 ? 10 : pageSize;

        // 041 起经 Credit_Log 联查过滤学生（学生列表侧有 IDX_D_CREDIT_LOG_STUDENT_TIME 背书）
        var query = _context.CreditAppeals
            .Join(_context.CreditLogs, a => a.CreditLogId, l => l.LogId, (a, l) => new { Appeal = a, Log = l })
            .Where(x => x.Log.StudentId == studentId)
            .Select(x => x.Appeal);
        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(a => a.CreateTime)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var dtoItems = new List<CreditAppealDto>(items.Count);
        foreach (var item in items)
        {
            dtoItems.Add(await ToDtoAsync(item, cancellationToken));
        }

        return new PagedResult<CreditAppealDto>
        {
            Items = dtoItems,
            Total = total,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<PagedResult<CreditAppealDto>> GetAllAsync(
        int accountId,
        string? status,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        // 鉴权：仅楼长/超管可查看全局申诉复核队列（与复核接口 DormAdmin 策略一致）
        var reviewerAdminId = await _userAccountRepository.GetAdminIdByAccountIdAsync(
            accountId,
            cancellationToken);
        if (string.IsNullOrWhiteSpace(reviewerAdminId))
        {
            throw new BusinessException(403, "当前账号未关联宿管身份，无法查看申诉队列", 403);
        }

        page = Math.Max(page, 1);
        pageSize = pageSize is < 1 or > 100 ? 10 : pageSize;

        var query = _context.CreditAppeals.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(a => a.Status == status);
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(a => a.CreateTime)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var dtoItems = new List<CreditAppealDto>(items.Count);
        foreach (var item in items)
        {
            dtoItems.Add(await ToDtoAsync(item, cancellationToken));
        }

        return new PagedResult<CreditAppealDto>
        {
            Items = dtoItems,
            Total = total,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<CreditAppealDto> ReviewAsync(
        int appealId,
        int accountId,
        ReviewCreditAppealRequest dto,
        CancellationToken cancellationToken)
    {
        // 复核人身份：accountId → D_User_Account.AdminId → 存 D_Admin.Admin_ID。
        // 不能取 Login_Name（Reviewed_By FK → D_Admin.Admin_ID），否则非种子环境复核必 500。
        var reviewerAdminId = await _userAccountRepository.GetAdminIdByAccountIdAsync(
            accountId,
            cancellationToken);
        if (string.IsNullOrWhiteSpace(reviewerAdminId))
        {
            throw new BusinessException(403, "当前账号未关联宿管身份，无法复核", 403);
        }

        var appeal = await _context.CreditAppeals
            .FirstOrDefaultAsync(a => a.AppealId == appealId, cancellationToken)
            ?? throw new BusinessException(404, "申诉不存在", StatusCodes.Status404NotFound);

        if (appeal.Status != StatusPending)
        {
            throw new BusinessException(400, "该申诉已复核，不能重复操作");
        }

        bool pass;
        if (dto.Result == "通过")
        {
            pass = true;
        }
        else if (dto.Result == "驳回")
        {
            pass = false;
        }
        else
        {
            throw new BusinessException(400, "复核结论只能是「通过」或「驳回」");
        }

        if (pass)
        {
            var log = await _context.CreditLogs.AsNoTracking()
                .FirstOrDefaultAsync(l => l.LogId == appeal.CreditLogId, cancellationToken)
                ?? throw new BusinessException(400, "被申诉的扣分明细不存在");
            var restoreScore = Math.Abs(log.ScoreChange);
            if (restoreScore <= 0)
            {
                throw new BusinessException(400, "被申诉的扣分明细分值无恢复必要");
            }

            await _creditService.RestoreAsync(
                log.StudentId,
                restoreScore,
                $"APPEAL-{appeal.AppealId}",
                $"申诉通过恢复（{appeal.Reason}）",
                cancellationToken);

            // D3 修复：RestoreAsync 的真实恢复路径会执行 ChangeTracker.Clear()，
            // 使本方法先前加载的 appeal 实体脱离跟踪；若不重新加载，随后的
            // SaveChangesAsync 不会生成 UPDATE，导致复核状态首次调用不落库。
            appeal = await _context.CreditAppeals
                .FirstAsync(a => a.AppealId == appealId, cancellationToken);
        }

        appeal.Status = pass ? StatusApproved : StatusRejected;
        appeal.ResultDesc = dto.Note;
        appeal.ReviewedBy = reviewerAdminId;
        appeal.ReviewTime = DateTime.Now;
        await _context.SaveChangesAsync(cancellationToken);

        // 041：申诉行不再携带学生号，收件人经 Credit_Log 反查（与通知/DTO 同源）
        // fail-soft：被申诉流水缺失（如已清理）时跳过学生通知，不阻断复核结果落库
        var appealStudentId = await GetAppealStudentIdAsync(appeal, cancellationToken);
        if (appealStudentId is not null)
        {
            await _notificationService.CreateAsync(new NotificationCreateDto
            {
                StudentId = appealStudentId,
                Title = pass ? "信用申诉已通过" : "信用申诉已驳回",
                Content = pass
                    ? $"您对扣分明细 {appeal.CreditLogId} 的申诉已通过，信用分已恢复。"
                    : $"您对扣分明细 {appeal.CreditLogId} 的申诉已被驳回：{dto.Note ?? "未说明"}",
                NotificationType = "信用"
            });
        }

        await _auditService.LogEventAsync(
            pass ? "信用申诉通过" : "信用申诉驳回",
            targetType: "CreditAppeal",
            targetId: appeal.AppealId.ToString(),
            details: dto.Note ?? appeal.Reason);

        // 申诉通过 → 违规联动：自动撤销对应违规 + 通知超管（可去治理页彻底删除）
        if (pass)
        {
            await RevokeLinkedViolationAsync(appeal, cancellationToken);
            await NotifySuperAdminsAsync(appeal, appealStudentId, cancellationToken);
        }

        return await ToDtoAsync(appeal, cancellationToken);
    }

    /// <summary>
    /// 申诉通过后：若被申诉扣分来自违规登记（扣分流水 EventKey=「违规-{违规ID}」），
    /// 自动把对应违规标记为「已撤销」，保持"申诉通过则违规不再有效"的数据一致。
    /// </summary>
    private async Task RevokeLinkedViolationAsync(CreditAppeal appeal, CancellationToken cancellationToken)
    {
        var log = await _context.CreditLogs.AsNoTracking()
            .FirstOrDefaultAsync(l => l.LogId == appeal.CreditLogId, cancellationToken);
        if (log?.EventKey?.StartsWith("违规-", StringComparison.Ordinal) != true)
        {
            return;
        }

        var suffix = log.EventKey["违规-".Length..];
        if (!int.TryParse(suffix, out var violationId))
        {
            return;
        }

        var violation = await _context.ViolationRecords
            .FirstOrDefaultAsync(v => v.RecordId == violationId, cancellationToken);
        if (violation is null || violation.Status == "已撤销")
        {
            return;
        }

        violation.Status = "已撤销";
        await _context.SaveChangesAsync(cancellationToken);

        await _auditService.LogEventAsync(
            "申诉通过自动撤销违规",
            targetType: "Violation",
            targetId: violationId.ToString(),
            details: $"申诉 #{appeal.AppealId} 通过，违规记录 {violationId} 已标记已撤销");
    }

    /// <summary>
    /// 041：申诉行不再存学生号，经 Credit_Log 反查归属学生。
    /// 流水缺失（如已清理）时返回 null——复核结果此时已落库，通知侧 fail-soft 跳过即可，
    /// 不抛 400 造成"状态已落库但请求失败"的矛盾（驳回路径不预检流水）。
    /// </summary>
    private async Task<string?> GetAppealStudentIdAsync(CreditAppeal appeal, CancellationToken cancellationToken)
    {
        return await _context.CreditLogs.AsNoTracking()
            .Where(l => l.LogId == appeal.CreditLogId)
            .Select(l => l.StudentId)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <summary>申诉通过后通知所有超管（fail-soft，不阻断复核主流程）。</summary>
    private async Task NotifySuperAdminsAsync(CreditAppeal appeal, string? studentId, CancellationToken cancellationToken)
    {
        try
        {
            var superAdminIds = await _context.Admins.AsNoTracking()
                .Where(a => a.RoleLevel == "超级管理员")
                .Select(a => a.AdminId)
                .ToListAsync(cancellationToken);

            foreach (var adminId in superAdminIds)
            {
                await _notificationService.CreateAsync(new NotificationCreateDto
                {
                    AdminId = adminId,
                    Title = "违规申诉已通过，待撤销违规",
                    Content = $"学生 {studentId} 的违规申诉已通过（扣分明细 #{appeal.CreditLogId}）。如确属误登记，请前往治理页删除对应违规。",
                    NotificationType = "信用"
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "申诉通过后通知超管失败，appealId={AppealId}", appeal.AppealId);
        }
    }

    private async Task<string> ResolveOwnStudentIdAsync(int accountId, CancellationToken cancellationToken)
    {
        var studentId = await _userAccountRepository.GetStudentIdByAccountIdAsync(
            accountId,
            cancellationToken);
        if (string.IsNullOrWhiteSpace(studentId))
        {
            throw new BusinessException(403, "当前账号不是学生身份", 403);
        }

        return studentId;
    }

    private async Task<CreditAppealDto> ToDtoAsync(CreditAppeal appeal, CancellationToken cancellationToken)
    {
        var log = await _context.CreditLogs.AsNoTracking()
            .FirstOrDefaultAsync(l => l.LogId == appeal.CreditLogId, cancellationToken);
        return new CreditAppealDto
        {
            AppealId = appeal.AppealId,
            CreditRecordId = appeal.CreditLogId,
            // 041：学生号经被申诉流水联查回填，API 响应 shape 不变
            StudentId = log?.StudentId ?? string.Empty,
            Reason = appeal.Reason,
            Status = appeal.Status,
            ResultDesc = appeal.ResultDesc,
            ReviewedBy = appeal.ReviewedBy,
            ReviewTime = appeal.ReviewTime,
            CreateTime = appeal.CreateTime,
            ScoreChange = log?.ScoreChange,
            CreditReason = log?.Reason
        };
    }
}
