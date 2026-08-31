using TemplateDormApi.DTO;
using TemplateDormApi.Repository;

namespace TemplateDormApi.Services;

public interface IViolationService
{
    Task<ViolationDto> CreateAsync(CreateViolationRequest request, string? recordBy, CancellationToken cancellationToken);
    Task<PagedResult<ViolationDto>> GetPagedAsync(ViolationQueryDto query, CancellationToken cancellationToken);
}

public sealed class ViolationService : IViolationService
{
    private readonly ViolationRepository _repository;
    private readonly ICreditService _creditService;

    public ViolationService(ViolationRepository repository, ICreditService creditService)
    {
        _repository = repository;
        _creditService = creditService;
    }

    public async Task<ViolationDto> CreateAsync(
        CreateViolationRequest request,
        string? recordBy,
        CancellationToken cancellationToken)
    {
        // 1) 违规先落库（repository 内部 SaveChanges），拿到 ViolationId 作为扣分 EventKey 幂等键
        var dto = await _repository.CreateAsync(request, recordBy, cancellationToken);

        // 2) 按类型映射扣分：违章电器 -10，其余（查寝未归/其他）-5
        var scoreChange = request.Type == "违章电器" ? -10 : -5;

        try
        {
            await _creditService.DeductAsync(new CreditDeductDto
            {
                StudentId = request.StudentId,
                ScoreChange = scoreChange,
                Reason = $"违规扣分（{request.Type}）",
                EventKey = $"违规-{dto.ViolationId}",
                FloorAtZero = true
            }, cancellationToken);
        }
        catch
        {
            // 补偿：DeductAsync 失败路径已 ChangeTracker.Clear()，必须重查再删
            var row = await _repository.FindByIdAsync((int)dto.ViolationId, cancellationToken);
            if (row is not null)
            {
                await _repository.DeleteAsync(row, cancellationToken);
            }
            throw; // 还原用户原始错误（如"学生账户不存在或已停用"）
        }

        return dto;
    }

    public Task<PagedResult<ViolationDto>> GetPagedAsync(
        ViolationQueryDto query,
        CancellationToken cancellationToken)
        => _repository.GetPagedAsync(query, cancellationToken);
}
