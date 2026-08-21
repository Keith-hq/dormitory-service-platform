using TemplateDormApi.DTO;
using TemplateDormApi.Exceptions;
using TemplateDormApi.Repository;
using Microsoft.AspNetCore.Http;

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

    public LateEntryService(LateEntryRepository repository, IStudentIdentityService identityService)
    {
        _repository = repository;
        _identityService = identityService;
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

        return await _repository.UpdateReasonAsync(entry, reason, cancellationToken);
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

        return await _repository.CreateAsync(request, cancellationToken);
    }
}
