using TemplateDormApi.DTO;
using TemplateDormApi.Exceptions;
using TemplateDormApi.Repository;
using Microsoft.AspNetCore.Http;

namespace TemplateDormApi.Services;

public interface IHygieneService
{
    Task<IReadOnlyList<HygieneRecordDto>> GetRoomRecordsAsync(
        long roomId,
        int? accountId,
        bool isDormAdmin,
        CancellationToken cancellationToken);
    Task<HygieneRecordDto> CreateAsync(int accountId, CreateHygieneRecordRequest request, CancellationToken cancellationToken);
    Task<HygieneRecordDto> UpdateAsync(long recordId, int accountId, UpdateHygieneRecordRequest request, CancellationToken cancellationToken);
    Task<IReadOnlyList<HygieneRankingDto>> GetRankingsAsync(HygieneRankingQueryDto query, int accountId, bool isDormAdmin, CancellationToken cancellationToken);
}

public sealed class HygieneService : IHygieneService
{
    private readonly HygieneRepository _repository;

    public HygieneService(HygieneRepository repository)
    {
        _repository = repository;
    }

    public async Task<IReadOnlyList<HygieneRecordDto>> GetRoomRecordsAsync(
        long roomId,
        int? accountId,
        bool isDormAdmin,
        CancellationToken cancellationToken)
    {
        if (!isDormAdmin)
        {
            if (!accountId.HasValue)
            {
                throw new BusinessException(
                    StatusCodes.Status401Unauthorized,
                    "用户身份无效",
                    StatusCodes.Status401Unauthorized);
            }

            var currentRoomId = await _repository.GetCurrentRoomIdAsync(
                accountId.Value,
                cancellationToken);
            if (!currentRoomId.HasValue)
            {
                throw new BusinessException(
                    StatusCodes.Status403Forbidden,
                    "当前没有有效住宿，无法查看卫生成绩",
                    StatusCodes.Status403Forbidden);
            }

            if (currentRoomId.Value != roomId)
            {
                throw new BusinessException(
                    StatusCodes.Status403Forbidden,
                    "无权查看其他房间的卫生成绩",
                    StatusCodes.Status403Forbidden);
            }
        }

        return await _repository.GetRoomRecordsAsync(roomId, cancellationToken);
    }

    public async Task<HygieneRecordDto> CreateAsync(
        int accountId,
        CreateHygieneRecordRequest request,
        CancellationToken cancellationToken)
    {
        var adminId = await _repository.GetAdminIdAsync(accountId, cancellationToken)
            ?? throw new BusinessException(403, "当前账户未关联宿管身份", StatusCodes.Status403Forbidden);
        return await _repository.CreateAsync(adminId, request, cancellationToken);
    }

    public async Task<HygieneRecordDto> UpdateAsync(
        long recordId,
        int accountId,
        UpdateHygieneRecordRequest request,
        CancellationToken cancellationToken)
    {
        var adminId = await _repository.GetAdminIdAsync(accountId, cancellationToken)
            ?? throw new BusinessException(403, "当前账户未关联宿管身份", StatusCodes.Status403Forbidden);

        var record = await _repository.FindByIdAsync(recordId, cancellationToken)
            ?? throw new BusinessException(404, "卫生评分记录不存在", StatusCodes.Status404NotFound);

        if (DateTime.Now > record.CheckDate.AddHours(24))
        {
            throw new BusinessException(409, "已超过 24 小时评分修改时限", StatusCodes.Status409Conflict);
        }

        return await _repository.UpdateAsync(record, request, cancellationToken);
    }

    public async Task<IReadOnlyList<HygieneRankingDto>> GetRankingsAsync(
        HygieneRankingQueryDto query,
        int accountId,
        bool isDormAdmin,
        CancellationToken cancellationToken)
    {
        var buildingId = await _repository.GetAccessibleBuildingIdAsync(accountId, isDormAdmin, cancellationToken)
            ?? throw new BusinessException(403, "当前账户没有可访问的宿舍楼", StatusCodes.Status403Forbidden);
        return await _repository.GetRankingsAsync(query, buildingId, cancellationToken);
    }
}
