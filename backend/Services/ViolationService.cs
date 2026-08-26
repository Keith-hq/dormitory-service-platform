using TemplateDormApi.DTO;
using TemplateDormApi.Repository;

namespace TemplateDormApi.Services;

public interface IViolationService
{
    Task<ViolationDto> CreateAsync(CreateViolationRequest request, string? recordBy, CancellationToken cancellationToken);
    Task<PagedResult<ViolationDto>> GetPagedAsync(ViolationQueryDto query, CancellationToken cancellationToken);
}

public sealed class ViolationService : IViolationService
{
    private readonly ViolationRepository _repository;

    public ViolationService(ViolationRepository repository)
    {
        _repository = repository;
    }

    public Task<ViolationDto> CreateAsync(CreateViolationRequest request, string? recordBy, CancellationToken cancellationToken)
        => _repository.CreateAsync(request, recordBy, cancellationToken);

    public Task<PagedResult<ViolationDto>> GetPagedAsync(
        ViolationQueryDto query,
        CancellationToken cancellationToken)
        => _repository.GetPagedAsync(query, cancellationToken);
}
