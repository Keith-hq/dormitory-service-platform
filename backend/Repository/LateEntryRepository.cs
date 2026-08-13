using TemplateDormApi.Data;
using TemplateDormApi.DTO;

namespace TemplateDormApi.Repository;

public sealed class LateEntryRepository : FrameworkRepositoryBase
{
    public LateEntryRepository(AppDbContext context) : base(context) { }

    public Task<PagedResult<LateEntryDto>> GetStudentEntriesAsync(
        string studentId,
        LateEntryQueryDto query,
        CancellationToken cancellationToken)
        => PendingAsync<PagedResult<LateEntryDto>>(
            "STU-13",
            "学生身份校验和分页查询待实现",
            cancellationToken);

    public Task<LateEntryDto> UpdateReasonAsync(
        long recordId,
        UpdateLateEntryReasonRequest request,
        CancellationToken cancellationToken)
        => PendingAsync<LateEntryDto>(
            "STU-14",
            "24 小时时限和记录归属校验待实现",
            cancellationToken);

    public Task<LateEntryDto> CreateAsync(
        CreateLateEntryRequest request,
        CancellationToken cancellationToken)
        => PendingAsync<LateEntryDto>(
            "DORM-31",
            "D_LATE_ENTRY 新增记录的主键生成方案待确认",
            cancellationToken);
}
