using TemplateDormApi.DTO;
using TemplateDormApi.Exceptions;
using TemplateDormApi.Repository;
using Microsoft.AspNetCore.Http;

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

    public async Task<HygieneRecordDto> UpdateAsync(
        long recordId,
        UpdateHygieneRecordRequest request,
        CancellationToken cancellationToken)
    {
        var record = await _repository.FindByIdAsync(recordId, cancellationToken)
            ?? throw new BusinessException(404, "卫生评分记录不存在", StatusCodes.Status404NotFound);

        if (DateTime.Now > record.CheckDate.AddHours(24))
        {
            throw new BusinessException(409, "已超过 24 小时评分修改时限", StatusCodes.Status409Conflict);
        }

        return await _repository.UpdateAsync(record, request, cancellationToken);
    }

    public Task<IReadOnlyList<HygieneRankingDto>> GetRankingsAsync(
        HygieneRankingQueryDto query,
        CancellationToken cancellationToken)
        => _repository.GetRankingsAsync(query, cancellationToken);
}
