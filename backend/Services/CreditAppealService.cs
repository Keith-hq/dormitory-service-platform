using Microsoft.EntityFrameworkCore;
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

    public CreditAppealService(
        AppDbContext context,
        UserAccountRepository userAccountRepository,
        ICreditService creditService,
        INotificationService notificationService,
        IAuditService auditService)
    {
        _context = context;
        _userAccountRepository = userAccountRepository;
        _creditService = creditService;
        _notificationService = notificationService;
        _auditService = auditService;
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
            CreditLogId = dto.CreditRecordId,
            StudentId = studentId,
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
        CancellationToken cancellationToken)
    {
        // 学生本人可查自己的申诉；宿管/超管由控制器策略放行（账号解析不到学生 = 管理员）
        var currentStudentId = await _userAccountRepository.GetStudentIdByAccountIdAsync(
            accountId,
            cancellationToken);
        if (currentStudentId is not null &&
            !string.Equals(currentStudentId, studentId, StringComparison.Ordinal))
        {
            throw new BusinessException(403, "无权查看他人申诉", 403);
        }

        page = Math.Max(page, 1);
        pageSize = pageSize is < 1 or > 100 ? 10 : pageSize;

        var query = _context.CreditAppeals.Where(a => a.StudentId == studentId);
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
        string reviewerAdminId,
        ReviewCreditAppealRequest dto,
        CancellationToken cancellationToken)
    {
        var appeal = await _context.CreditAppeals
            .FirstOrDefaultAsync(a => a.AppealId == appealId, cancellationToken)
            ?? throw new BusinessException(404, "申诉不存在");

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
                appeal.StudentId,
                restoreScore,
                $"APPEAL-{appeal.AppealId}",
                $"申诉通过恢复（{appeal.Reason}）",
                cancellationToken);
        }

        appeal.Status = pass ? StatusApproved : StatusRejected;
        appeal.ResultDesc = dto.Note;
        appeal.ReviewedBy = reviewerAdminId;
        appeal.ReviewTime = DateTime.Now;
        await _context.SaveChangesAsync(cancellationToken);

        await _notificationService.CreateAsync(new NotificationCreateDto
        {
            StudentId = appeal.StudentId,
            Title = pass ? "信用申诉已通过" : "信用申诉已驳回",
            Content = pass
                ? $"您对扣分明细 {appeal.CreditLogId} 的申诉已通过，信用分已恢复。"
                : $"您对扣分明细 {appeal.CreditLogId} 的申诉已被驳回：{dto.Note ?? "未说明"}",
            NotificationType = "信用"
        });

        await _auditService.LogEventAsync(
            pass ? "信用申诉通过" : "信用申诉驳回",
            targetType: "CreditAppeal",
            targetId: appeal.AppealId.ToString(),
            details: dto.Note ?? appeal.Reason);

        return await ToDtoAsync(appeal, cancellationToken);
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
            StudentId = appeal.StudentId,
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
