using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using TemplateDormApi.DTO;
using TemplateDormApi.Exceptions;
using TemplateDormApi.Models;
using TemplateDormApi.Repository;

namespace TemplateDormApi.Services;

/// <summary>
/// 访客授权业务逻辑接口
/// </summary>
public interface IVisitorService
{
    Task<VisitorAuthorization> ApplyAsync(string studentId, VisitorApplyRequest dto);
    Task<PagedResult<VisitorAuthorization>> GetMyListAsync(string studentId, int page, int pageSize);
    Task<VisitorAuthorization> GetCredentialAsync(int authId, string currentStudentId);
    Task<VisitorAuthorization> RevokeAsync(int authId, string currentStudentId);
    Task<int> ExpireAsync();
}

/// <summary>
/// 访客授权业务逻辑实现
/// </summary>
public class VisitorService : IVisitorService
{
    private readonly VisitorRepository _repository;
    private readonly ICreditService _creditService;
    private readonly INotificationService _notificationService;
    private readonly ILogger<VisitorService> _logger;

    public VisitorService(
        VisitorRepository repository,
        ICreditService creditService,
        INotificationService notificationService,
        ILogger<VisitorService> logger)
    {
        _repository = repository;
        _creditService = creditService;
        _notificationService = notificationService;
        _logger = logger;
    }

    public async Task<VisitorAuthorization> ApplyAsync(string studentId, VisitorApplyRequest dto)
    {
        var now = DateTime.Now;
        var endTime = dto.EndTime!.Value;
        if (endTime <= now)
            throw new BusinessException(400, "授权截止时间必须晚于当前时间");

        // 信用冻结检查：分数 < 60（阈值在 CreditService，预约/借物由存储过程拦截，此处补齐访客口子）
        var credit = await _creditService.GetStatusAsync(studentId, CancellationToken.None);
        if (credit.IsFrozen)
            throw new BusinessException(400, "信用分低于 60 分，访客申请已冻结，请恢复信用后再申请");

        // Room_ID 为 NOT NULL：只读查询当前学生房间，无在住房间则不允许申请
        var roomId = await _repository.GetActiveRoomIdAsync(studentId)
            ?? throw new BusinessException(400, "当前无在住房间，无法申请访客授权");

        var auth = new VisitorAuthorization
        {
            StudentId = studentId,
            RoomId = (int)roomId,
            VisitorName = dto.VisitorName,
            VisitReason = dto.VisitReason,
            AuthorizationToken = "VSR_" + Guid.NewGuid().ToString("N").ToUpper().Substring(0, 12),
            ExpiresTime = endTime,
            Status = "有效",
            CreateTime = now
        };
        var saved = await _repository.AddAsync(auth);

        await TryNotifyAsync(studentId, (int)roomId, dto, endTime);

        return saved;
    }

    public async Task<PagedResult<VisitorAuthorization>> GetMyListAsync(
        string studentId, int page, int pageSize)
    {
        var (items, total) = await _repository.GetPagedByStudentAsync(studentId, page, pageSize);
        return new PagedResult<VisitorAuthorization>
        {
            Items = items,
            Total = total,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<VisitorAuthorization> GetCredentialAsync(int authId, string currentStudentId)
    {
        var auth = await _repository.GetByIdAsync(authId)
            ?? throw new BusinessException(404, "授权记录不存在", StatusCodes.Status404NotFound);

        if (auth.StudentId != currentStudentId)
            throw new BusinessException(403, "无权查看他人的授权凭证", StatusCodes.Status403Forbidden);

        return auth;
    }

    public async Task<VisitorAuthorization> RevokeAsync(int authId, string currentStudentId)
    {
        var auth = await _repository.GetByIdAsync(authId)
            ?? throw new BusinessException(404, "授权记录不存在", StatusCodes.Status404NotFound);

        if (auth.StudentId != currentStudentId)
            throw new BusinessException(403, "无权撤销他人的授权", StatusCodes.Status403Forbidden);

        if (auth.Status != "有效")
            throw new BusinessException(400, $"当前状态为「{auth.Status}」，无法撤销");

        auth.Status = "已撤销";
        return await _repository.UpdateAsync(auth);
    }

    /// <summary>供内部调度触发：将已到期的有效访客授权批量置为「已过期」。</summary>
    public async Task<int> ExpireAsync()
        => await _repository.ExpireAsync(DateTime.Now);

    /// <summary>
    /// 访客申请成功后的通知（fail-soft，不阻断主流程）：
    /// 通知学生本人 + 该学生所在楼栋宿管（体现"一端写入、他端接收"连通性）。
    /// </summary>
    private async Task TryNotifyAsync(
        string studentId,
        int roomId,
        VisitorApplyRequest dto,
        DateTime endTime)
    {
        try
        {
            await _notificationService.CreateAsync(new NotificationCreateDto
            {
                StudentId = studentId,
                Title = "访客授权已申请",
                Content = $"您的访客码已生成，访客「{dto.VisitorName}」可凭码进入楼栋，授权截止 {endTime:yyyy-MM-dd HH:mm}。",
                NotificationType = "访客"
            });

            var adminId = await _repository.GetBuildingAdminIdAsync(roomId, CancellationToken.None);
            if (!string.IsNullOrWhiteSpace(adminId))
            {
                await _notificationService.CreateAsync(new NotificationCreateDto
                {
                    AdminId = adminId,
                    Title = "新增访客授权待值守",
                    Content = $"学生 {studentId} 申请了访客授权（访客：{dto.VisitorName}，截止 {endTime:yyyy-MM-dd HH:mm}），请值守台留意。",
                    NotificationType = "访客"
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "访客授权通知投递失败，studentId={StudentId}", studentId);
        }
    }
}
