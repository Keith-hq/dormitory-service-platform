using TemplateDormApi.Data;
using TemplateDormApi.DTO;

namespace TemplateDormApi.Repository;

public sealed class AccessRepository : FrameworkRepositoryBase
{
    public AccessRepository(AppDbContext context) : base(context) { }

    public Task<PagedResult<AccessLogDto>> GetLogsAsync(
        AccessLogQueryDto query,
        CancellationToken cancellationToken)
        => PendingAsync<PagedResult<AccessLogDto>>(
            "ACCESS-01",
            "门禁分页筛选及离线数据判定待实现",
            cancellationToken);

    public Task<IReadOnlyList<AccessDensityDto>> GetDensityAsync(
        AccessDensityQueryDto query,
        CancellationToken cancellationToken)
        => PendingAsync<IReadOnlyList<AccessDensityDto>>(
            "ACCESS-02",
            "当前在楼人数与额定容量的统计口径待确认",
            cancellationToken);
}
