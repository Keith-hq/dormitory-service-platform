using TemplateDormApi.DTO;
using TemplateDormApi.Repository;

namespace TemplateDormApi.Services;

public interface IAccessService
{
    Task<PagedResult<AccessLogDto>> GetLogsAsync(AccessLogQueryDto query, CancellationToken cancellationToken);
    Task<IReadOnlyList<AccessDensityDto>> GetDensityAsync(AccessDensityQueryDto query, CancellationToken cancellationToken);
}

public sealed class AccessService : IAccessService
{
    private readonly AccessRepository _repository;

    public AccessService(AccessRepository repository)
    {
        _repository = repository;
    }

    public Task<PagedResult<AccessLogDto>> GetLogsAsync(
        AccessLogQueryDto query,
        CancellationToken cancellationToken)
        => _repository.GetLogsAsync(query, cancellationToken);

    public Task<IReadOnlyList<AccessDensityDto>> GetDensityAsync(
        AccessDensityQueryDto query,
        CancellationToken cancellationToken)
        => _repository.GetDensityAsync(query, cancellationToken);
}
