using TemplateDormApi.DTO;
using TemplateDormApi.Repository;

namespace TemplateDormApi.Services;

public interface IVisitorRegistryService
{
    Task<VisitorRegistryDto> CreateAsync(CreateVisitorRegistryRequest request, CancellationToken cancellationToken);
    Task<VisitorRegistryDto> VerifyAsync(long registryId, VerifyVisitorRegistryRequest request, CancellationToken cancellationToken);
    Task<VisitorRegistryDto> RecordExitAsync(long registryId, CancellationToken cancellationToken);
}

public sealed class VisitorRegistryService : IVisitorRegistryService
{
    private readonly VisitorRegistryRepository _repository;

    public VisitorRegistryService(VisitorRegistryRepository repository)
    {
        _repository = repository;
    }

    public Task<VisitorRegistryDto> CreateAsync(
        CreateVisitorRegistryRequest request,
        CancellationToken cancellationToken)
        => _repository.CreateAsync(request, cancellationToken);

    public Task<VisitorRegistryDto> VerifyAsync(
        long registryId,
        VerifyVisitorRegistryRequest request,
        CancellationToken cancellationToken)
        => _repository.VerifyAsync(registryId, request, cancellationToken);

    public Task<VisitorRegistryDto> RecordExitAsync(long registryId, CancellationToken cancellationToken)
        => _repository.RecordExitAsync(registryId, cancellationToken);
}
