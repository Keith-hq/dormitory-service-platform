using TemplateDormApi.DTO;
using TemplateDormApi.Repository;

namespace TemplateDormApi.Services;

public interface IRepairService
{
    Task<RepairTicketDto> CreateAsync(SubmitRepairTicketRequest request, CancellationToken cancellationToken);
    Task<PagedResult<RepairTicketDto>> GetStudentTicketsAsync(string studentId, int accountId, RepairTicketQueryDto query, CancellationToken cancellationToken);
    Task<RepairTicketDto> GetByIdAsync(long ticketId, CancellationToken cancellationToken);
    Task<RepairTicketDto> CancelAsync(long ticketId, CancellationToken cancellationToken);
    Task<IReadOnlyList<RepairAttachmentDto>> AddAttachmentsAsync(long ticketId, UploadRepairAttachmentsRequest request, CancellationToken cancellationToken);
}

public sealed class RepairService : IRepairService
{
    private readonly RepairRepository _repository;
    private readonly IStudentIdentityService _identityService;

    public RepairService(RepairRepository repository, IStudentIdentityService identityService)
    {
        _repository = repository;
        _identityService = identityService;
    }

    public Task<RepairTicketDto> CreateAsync(SubmitRepairTicketRequest request, CancellationToken cancellationToken)
        => _repository.CreateAsync(request, cancellationToken);

    public async Task<PagedResult<RepairTicketDto>> GetStudentTicketsAsync(
        string studentId,
        int accountId,
        RepairTicketQueryDto query,
        CancellationToken cancellationToken)
    {
        await _identityService.EnsureOwnStudentIdAsync(accountId, studentId, cancellationToken);
        return await _repository.GetStudentTicketsAsync(studentId, query, cancellationToken);
    }

    public Task<RepairTicketDto> GetByIdAsync(long ticketId, CancellationToken cancellationToken)
        => _repository.GetByIdAsync(ticketId, cancellationToken);

    public Task<RepairTicketDto> CancelAsync(long ticketId, CancellationToken cancellationToken)
        => _repository.CancelAsync(ticketId, cancellationToken);

    public Task<IReadOnlyList<RepairAttachmentDto>> AddAttachmentsAsync(
        long ticketId,
        UploadRepairAttachmentsRequest request,
        CancellationToken cancellationToken)
        => _repository.AddAttachmentsAsync(ticketId, request, cancellationToken);
}
