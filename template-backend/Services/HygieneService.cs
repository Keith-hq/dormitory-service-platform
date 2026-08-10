using TemplateDormApi.DTO;
using TemplateDormApi.Repository;

namespace TemplateDormApi.Services;

public interface IHygieneService
{
    Task<IReadOnlyList<HygieneRecordDto>> GetRoomRecordsAsync(long roomId, CancellationToken cancellationToken);
    Task<HygieneRecordDto> CreateAsync(CreateHygieneRecordRequest request, CancellationToken cancellationToken);
    Task<HygieneRecordDto> UpdateAsync(long recordId, UpdateHygieneRecordRequest request, CancellationToken cancellationToken);
    Task<IReadOnlyList<HygieneRankingDto>> GetRankingsAsync(HygieneRankingQueryDto query, CancellationToken cancellationToken);
}

public sealed class HygieneService : IHygieneService
{
    private readonly HygieneRepository _repository;

    public HygieneService(HygieneRepository repository)
    {
        _repository = repository;
    }

    public Task<IReadOnlyList<HygieneRecordDto>> GetRoomRecordsAsync(long roomId, CancellationToken cancellationToken)
        => _repository.GetRoomRecordsAsync(roomId, cancellationToken);

    public Task<HygieneRecordDto> CreateAsync(CreateHygieneRecordRequest request, CancellationToken cancellationToken)
        => _repository.CreateAsync(request, cancellationToken);

    public Task<HygieneRecordDto> UpdateAsync(
        long recordId,
        UpdateHygieneRecordRequest request,
        CancellationToken cancellationToken)
        => _repository.UpdateAsync(recordId, request, cancellationToken);

    public Task<IReadOnlyList<HygieneRankingDto>> GetRankingsAsync(
        HygieneRankingQueryDto query,
        CancellationToken cancellationToken)
        => _repository.GetRankingsAsync(query, cancellationToken);
}
