using TemplateDormApi.Data;
using TemplateDormApi.DTO;

namespace TemplateDormApi.Repository;

public sealed class VisitorRegistryRepository : FrameworkRepositoryBase
{
    public VisitorRegistryRepository(AppDbContext context) : base(context) { }

    public Task<VisitorRegistryDto> CreateAsync(
        CreateVisitorRegistryRequest request,
        CancellationToken cancellationToken)
        => PendingAsync<VisitorRegistryDto>(
            "VST-01",
            "现有表仅保存访客授权，缺少门岗登记记录存储结构",
            cancellationToken);

    public Task<VisitorRegistryDto> VerifyAsync(
        long registryId,
        VerifyVisitorRegistryRequest request,
        CancellationToken cancellationToken)
        => PendingAsync<VisitorRegistryDto>(
            "VST-02",
            "需确认是否复用现有 VisitorService 及授权令牌校验逻辑",
            cancellationToken);

    public Task<VisitorRegistryDto> RecordExitAsync(
        long registryId,
        CancellationToken cancellationToken)
        => PendingAsync<VisitorRegistryDto>(
            "VST-03",
            "现有表缺少访客离开时间和登记状态字段",
            cancellationToken);
}
