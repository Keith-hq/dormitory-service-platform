using TemplateDormApi.DTO;
using TemplateDormApi.Repository;

namespace TemplateDormApi.Services;

public interface ILateEntryService
{
    Task<PagedResult<LateEntryDto>> GetStudentEntriesAsync(string studentId, LateEntryQueryDto query, CancellationToken cancellationToken);
    Task<LateEntryDto> UpdateReasonAsync(long recordId, UpdateLateEntryReasonRequest request, CancellationToken cancellationToken);
    Task<LateEntryDto> CreateAsync(CreateLateEntryRequest request, CancellationToken cancellationToken);
}

public sealed class LateEntryService : ILateEntryService
{
    private readonly LateEntryRepository _repository;

    public LateEntryService(LateEntryRepository repository)
    {
        _repository = repository;
    }

    public Task<PagedResult<LateEntryDto>> GetStudentEntriesAsync(
        string studentId,
        LateEntryQueryDto query,
        CancellationToken cancellationToken)
        => _repository.GetStudentEntriesAsync(studentId, query, cancellationToken);

    public Task<LateEntryDto> UpdateReasonAsync(
        long recordId,
        UpdateLateEntryReasonRequest request,
        CancellationToken cancellationToken)
        => _repository.UpdateReasonAsync(recordId, request, cancellationToken);

    public Task<LateEntryDto> CreateAsync(CreateLateEntryRequest request, CancellationToken cancellationToken)
        => _repository.CreateAsync(request, cancellationToken);
}
