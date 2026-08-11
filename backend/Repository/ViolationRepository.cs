using TemplateDormApi.Data;
using TemplateDormApi.DTO;

namespace TemplateDormApi.Repository;

public sealed class ViolationRepository : FrameworkRepositoryBase
{
    public ViolationRepository(AppDbContext context) : base(context) { }

    public Task<ViolationDto> CreateAsync(
        CreateViolationRequest request,
        CancellationToken cancellationToken)
        => PendingAsync<ViolationDto>(
            "VIOL-01",
            "D_VIOLATION_RECORD 缺少 DETAIL/RECORD_BY 字段，且主键生成方案待确认",
            cancellationToken);

    public Task<PagedResult<ViolationDto>> GetPagedAsync(
        ViolationQueryDto query,
        CancellationToken cancellationToken)
        => PendingAsync<PagedResult<ViolationDto>>(
            "VIOL-02",
            "按楼栋筛选所需关联和返回字段口径待确认",
            cancellationToken);
}
