using TemplateDormApi.Data;
using TemplateDormApi.DTO;

namespace TemplateDormApi.Repository;

public sealed class HygieneRepository : FrameworkRepositoryBase
{
    public HygieneRepository(AppDbContext context) : base(context) { }

    public Task<IReadOnlyList<HygieneRecordDto>> GetRoomRecordsAsync(
        long roomId,
        CancellationToken cancellationToken)
        => PendingAsync<IReadOnlyList<HygieneRecordDto>>(
            "STU-18",
            "卫生评分与评语组合读取待实现",
            cancellationToken);

    public Task<HygieneRecordDto> CreateAsync(
        CreateHygieneRecordRequest request,
        CancellationToken cancellationToken)
        => PendingAsync<HygieneRecordDto>(
            "DORM-32",
            "卫生记录和评语主键生成方案待确认",
            cancellationToken);

    public Task<HygieneRecordDto> UpdateAsync(
        long recordId,
        UpdateHygieneRecordRequest request,
        CancellationToken cancellationToken)
        => PendingAsync<HygieneRecordDto>(
            "DORM-33",
            "24 小时修改窗口及评语更新规则待实现",
            cancellationToken);

    public Task<IReadOnlyList<HygieneRankingDto>> GetRankingsAsync(
        HygieneRankingQueryDto query,
        CancellationToken cancellationToken)
        => PendingAsync<IReadOnlyList<HygieneRankingDto>>(
            "DORM-34",
            "月度排名统计口径待确认",
            cancellationToken);
}
