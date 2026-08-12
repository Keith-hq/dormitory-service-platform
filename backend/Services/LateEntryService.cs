using TemplateDormApi.DTO;
using TemplateDormApi.Repository;

namespace TemplateDormApi.Services;

public interface ILateEntryService
{
    Task<PagedResult<LateEntryDto>> GetStudentEntriesAsync(string studentId, int accountId, LateEntryQueryDto query, CancellationToken cancellationToken);
    Task<LateEntryDto> UpdateReasonAsync(long recordId, UpdateLateEntryReasonRequest request, CancellationToken cancellationToken);
    Task<LateEntryDto> CreateAsync(CreateLateEntryRequest request, CancellationToken cancellationToken);
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

    public Task<LateEntryDto> UpdateReasonAsync(
        long recordId,
        UpdateLateEntryReasonRequest request,
        CancellationToken cancellationToken)
        => _repository.UpdateReasonAsync(recordId, request, cancellationToken);

    public Task<LateEntryDto> CreateAsync(CreateLateEntryRequest request, CancellationToken cancellationToken)
        => _repository.CreateAsync(request, cancellationToken);
}
