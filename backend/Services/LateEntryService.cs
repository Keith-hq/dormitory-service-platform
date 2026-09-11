using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using TemplateDormApi.DTO;
using TemplateDormApi.Exceptions;
using TemplateDormApi.Repository;

namespace TemplateDormApi.Services;

public interface ILateEntryService
{
    Task<PagedResult<LateEntryDto>> GetStudentEntriesAsync(string studentId, int accountId, LateEntryQueryDto query, CancellationToken cancellationToken);
    Task<LateEntryDto> UpdateReasonAsync(long recordId, int accountId, UpdateLateEntryReasonRequest request, CancellationToken cancellationToken);
    Task<LateEntryDto> CreateAsync(int accountId, CreateLateEntryRequest request, CancellationToken cancellationToken);
}

public sealed class LateEntryService : ILateEntryService
{
    private readonly LateEntryRepository _repository;
    private readonly IStudentIdentityService _identityService;
    private readonly INotificationService _notificationService;
    private readonly ILogger<LateEntryService> _logger;

    public LateEntryService(
        LateEntryRepository repository,
        IStudentIdentityService identityService,
        INotificationService notificationService,
        ILogger<LateEntryService> logger)
    {
        _repository = repository;
        _identityService = identityService;
        _notificationService = notificationService;
        _logger = logger;
    }

    public async Task<PagedResult<LateEntryDto>> GetStudentEntriesAsync(
        string studentId,
        int accountId,
        LateEntryQueryDto query,
        CancellationToken cancellationToken)
    {
        await _identityService.EnsureOwnStudentIdAsync(accountId, studentId, cancellationToken);
        return await _repository.GetStudentEntriesAsync(studentId, query, cancellationToken);
    }

    public async Task<LateEntryDto> UpdateReasonAsync(
        long recordId,
        int accountId,
        UpdateLateEntryReasonRequest request,
        CancellationToken cancellationToken)
    {
        var entry = await _repository.FindByIdAsync(recordId, cancellationToken)
            ?? throw new BusinessException(404, "晚归记录不存在", StatusCodes.Status404NotFound);

        await _identityService.EnsureOwnStudentIdAsync(
            accountId,
            entry.StudentId ?? string.Empty,
            cancellationToken);

        if (DateTime.Now > entry.ReturnTime.AddHours(24))
        {
            throw new BusinessException(409, "已超过 24 小时补充说明时限", StatusCodes.Status409Conflict);
        }

        var reason = request.Reason.Trim();
        if (reason.Length == 0)
        {
            throw new BusinessException(400, "晚归说明不能为空");
        }

        var updated = await _repository.UpdateReasonAsync(entry, reason, cancellationToken);
        await TryNotifyBuildingDormAsync(entry.StudentId, updated, cancellationToken);
        return updated;
    }

    /// <summary>
    /// 学生补充晚归说明后通知其所住楼栋的楼长核对（fail-soft，不阻断学生侧补充主流程）。
    /// 与宿管登记时通知学生（<see cref="TryNotifyStudentAsync"/>）构成双向闭环：
    /// 学生端界面承诺「晚归说明会进入宿管核对流程」，此处即该流程的唯一送达通道
    /// ——宿管端没有晚归记录列表接口（见 ApiFrameworkContractTests 锁定的路由集）。
    /// </summary>
    private async Task TryNotifyBuildingDormAsync(
        string? studentId,
        LateEntryDto entry,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(studentId))
        {
            return;
        }

        IReadOnlyList<string> adminIds;
        try
        {
            adminIds = await _repository.GetBuildingDormAdminIdsAsync(studentId, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "晚归说明通知收件人解析失败，studentId={StudentId}", studentId);
            return;
        }

        if (adminIds.Count == 0)
        {
            // 无在住床位 / 房间未关联楼栋 / 该楼未配楼长：通知无处投递。
            // 通知是学生说明的唯一送达通道，静默丢失会让「宿管核对流程」整体失效，故显式告警。
            _logger.LogWarning(
                "晚归说明无收件楼长，学生说明未送达宿管，studentId={StudentId}, recordId={RecordId}",
                studentId,
                entry.RecordId);
            return;
        }

        foreach (var adminId in adminIds)
        {
            try
            {
                await _notificationService.CreateAsync(new NotificationCreateDto
                {
                    AdminId = adminId,
                    Title = "学生已补充晚归说明",
                    Content = $"学生 {studentId} 已就 {entry.RecordTime:MM-dd HH:mm} 的晚归记录补充说明，请核对。"
                              + $"补充内容：{entry.Reason}",
                    NotificationType = "系统"
                });
            }
            catch (BusinessException ex)
            {
                // 该楼长无有效账户等业务性失败 → 跳过该收件人，不影响同楼其他楼长与补交流程
                _logger.LogWarning(ex, "晚归说明通知跳过，adminId={AdminId}", adminId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "晚归说明通知投递失败，adminId={AdminId}", adminId);
            }
        }
    }

    public async Task<LateEntryDto> CreateAsync(
        int accountId,
        CreateLateEntryRequest request,
        CancellationToken cancellationToken)
    {
        var adminId = await _repository.GetAdminIdAsync(accountId, cancellationToken);
        if (adminId is null)
        {
            throw new BusinessException(403, "当前账户未关联宿管身份", StatusCodes.Status403Forbidden);
        }

        if (request.RecordTime.TimeOfDay < new TimeSpan(23, 30, 0))
        {
            throw new BusinessException(400, "仅登记 23:30 后的晚归记录");
        }
        if (request.RecordTime > DateTime.Now.AddMinutes(5))
        {
            throw new BusinessException(400, "晚归时间不能晚于当前时间");
        }
        // 与 UpdateReasonAsync 的 24 小时补充时限对齐：登记一条超过 24 小时的记录，
        // 学生已无法补充说明（会被 409 拒绝），且登记通知承诺的「24 小时内补充」当场失效。
        if (request.RecordTime < DateTime.Now.AddHours(-24))
        {
            throw new BusinessException(400, "晚归时间不能早于 24 小时前");
        }

        var result = await _repository.CreateAsync(request, cancellationToken);
        // 现场说明随通知投递（见 LateEntryRepository.CreateAsync：Reason 不再由登记写入）
        await TryNotifyStudentAsync(request.StudentId, result, request.Reason, cancellationToken);
        return result;
    }

    /// <summary>
    /// 宿管登记晚归后通知学生本人（fail-soft，不阻断登记主流程）。
    /// 宿管的现场说明在此随通知投递：D_Late_Entry.Reason 只有一列且已判给学生补充说明，
    /// 现场说明若写进该列会被学生的 PUT 覆盖，故改由通知行留档（不可变，可回溯）。
    /// </summary>
    private async Task TryNotifyStudentAsync(
        string studentId,
        LateEntryDto entry,
        string? sceneNote,
        CancellationToken cancellationToken)
    {
        try
        {
            var content = $"您有一条晚归记录（{entry.RecordTime:MM-dd HH:mm}），请在 24 小时内补充说明。";
            if (!string.IsNullOrWhiteSpace(sceneNote))
            {
                content += $"宿管现场说明：{sceneNote.Trim()}";
            }

            await _notificationService.CreateAsync(new NotificationCreateDto
            {
                StudentId = studentId,
                Title = "晚归登记提醒",
                Content = content,
                NotificationType = "系统"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "晚归登记通知投递失败，studentId={StudentId}", studentId);
        }
    }
}
