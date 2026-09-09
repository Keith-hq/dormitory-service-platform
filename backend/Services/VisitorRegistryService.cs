using TemplateDormApi.DTO;
using TemplateDormApi.Models;
using TemplateDormApi.Repository;

namespace TemplateDormApi.Services;

public interface IVisitorRegistryService
{
    Task<VisitorRegistryDto> CreateAsync(CreateVisitorRegistryRequest request, CancellationToken cancellationToken);
    Task<VisitorRegistryDto> VerifyAsync(long registryId, VerifyVisitorRegistryRequest request, CancellationToken cancellationToken);
    Task<VisitorRegistryDto> RecordExitAsync(long registryId, CancellationToken cancellationToken);
    Task<IReadOnlyList<VisitorRegistryDto>> GetActiveAsync(CancellationToken cancellationToken);
}

/// <summary>
/// 门岗登记服务（VST-01/02/03）：登记到访 → 扫码核验 → 离开记录。
/// </summary>
public sealed class VisitorRegistryService : IVisitorRegistryService
{
    private readonly VisitorRegistryRepository _repository;

    public VisitorRegistryService(VisitorRegistryRepository repository)
    {
        _repository = repository;
    }

    public async Task<VisitorRegistryDto> CreateAsync(
        CreateVisitorRegistryRequest request,
        CancellationToken cancellationToken)
    {
        var entity = await _repository.CreateAsync(request, cancellationToken);
        return ToDto(entity);
    }

    public async Task<VisitorRegistryDto> VerifyAsync(
        long registryId,
        VerifyVisitorRegistryRequest request,
        CancellationToken cancellationToken)
    {
        var entity = await _repository.VerifyAsync(registryId, request.QrToken, cancellationToken);
        return ToDto(entity);
    }

    public async Task<VisitorRegistryDto> RecordExitAsync(
        long registryId,
        CancellationToken cancellationToken)
    {
        var entity = await _repository.RecordExitAsync(registryId, cancellationToken);
        return ToDto(entity);
    }

    public async Task<IReadOnlyList<VisitorRegistryDto>> GetActiveAsync(
        CancellationToken cancellationToken)
    {
        var items = await _repository.GetActiveAsync(cancellationToken);
        return items.Select(ToDto).ToList();
    }

    private static VisitorRegistryDto ToDto(VisitorRegistry registry) => new()
    {
        RegistryId = registry.RegistryId,
        QrToken = registry.QrToken,
        VisitorName = registry.VisitorName,
        Phone = registry.Phone,
        StudentId = registry.StudentId,
        EnterTime = registry.EnterTime,
        ExitTime = registry.ExitTime,
        Status = registry.Status
    };
}
