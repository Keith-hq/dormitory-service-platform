using TemplateDormApi.DTO;
using TemplateDormApi.Repository;

namespace TemplateDormApi.Services;

public interface IRepairService
{
    Task<RepairTicketDto> CreateAsync(int accountId, SubmitRepairTicketRequest request, CancellationToken cancellationToken);
    Task<PagedResult<RepairTicketDto>> GetStudentTicketsAsync(string studentId, int accountId, RepairTicketQueryDto query, CancellationToken cancellationToken);
    Task<RepairTicketDto> GetByIdAsync(long ticketId, int accountId, CancellationToken cancellationToken);
    Task<RepairTicketDto> CancelAsync(long ticketId, int accountId, CancellationToken cancellationToken);
    Task<IReadOnlyList<RepairAttachmentDto>> AddAttachmentsAsync(long ticketId, int accountId, UploadRepairAttachmentsRequest request, CancellationToken cancellationToken);
}

public sealed class RepairService : IRepairService
{
    private readonly RepairRepository _repository;
    private readonly IStudentIdentityService _identityService;
    private readonly IFileStorageService _fileStorageService;

    public RepairService(
        RepairRepository repository,
        IStudentIdentityService identityService,
        IFileStorageService fileStorageService)
    {
        _repository = repository;
        _identityService = identityService;
        _fileStorageService = fileStorageService;
    }

    public async Task<RepairTicketDto> CreateAsync(
        int accountId,
        SubmitRepairTicketRequest request,
        CancellationToken cancellationToken)
    {
        var studentId = await _repository.GetStudentIdAsync(accountId, cancellationToken)
            ?? throw new TemplateDormApi.Exceptions.BusinessException(403, "当前账户未关联有效学生身份", 403);
        return await _repository.CreateAsync(studentId, request, cancellationToken);
    }

    public async Task<PagedResult<RepairTicketDto>> GetStudentTicketsAsync(
        string studentId,
        int accountId,
        RepairTicketQueryDto query,
        CancellationToken cancellationToken)
    {
        await _identityService.EnsureOwnStudentIdAsync(accountId, studentId, cancellationToken);
        return await _repository.GetStudentTicketsAsync(studentId, query, cancellationToken);
    }

    public async Task<RepairTicketDto> GetByIdAsync(long ticketId, int accountId, CancellationToken cancellationToken)
    {
        var ticket = await _repository.FindByIdAsync(ticketId, cancellationToken)
            ?? throw new TemplateDormApi.Exceptions.BusinessException(404, "报修工单不存在", 404);
        await _identityService.EnsureOwnStudentIdAsync(accountId, ticket.StudentId ?? string.Empty, cancellationToken);
        return RepairRepository.ToDto(ticket);
    }

    public async Task<RepairTicketDto> CancelAsync(long ticketId, int accountId, CancellationToken cancellationToken)
    {
        var ticket = await _repository.FindByIdAsync(ticketId, cancellationToken)
            ?? throw new TemplateDormApi.Exceptions.BusinessException(404, "报修工单不存在", 404);
        await _identityService.EnsureOwnStudentIdAsync(accountId, ticket.StudentId ?? string.Empty, cancellationToken);

        if (ticket.Status is not ("待处理" or "已派单"))
        {
            throw new TemplateDormApi.Exceptions.BusinessException(409, "当前工单状态不允许撤销", 409);
        }
        if (DateTime.Now > ticket.SubmitTime.AddMinutes(10))
        {
            throw new TemplateDormApi.Exceptions.BusinessException(409, "已超过 10 分钟撤销时限", 409);
        }
        if (ticket.Status == "已派单" && ticket.Log is not null)
        {
            throw new TemplateDormApi.Exceptions.BusinessException(409, "维修已开始，不能撤销工单", 409);
        }

        return await _repository.CancelAsync(ticket, cancellationToken);
    }

    public async Task<IReadOnlyList<RepairAttachmentDto>> AddAttachmentsAsync(
        long ticketId,
        int accountId,
        UploadRepairAttachmentsRequest request,
        CancellationToken cancellationToken)
    {
        var ticket = await _repository.FindByIdAsync(ticketId, cancellationToken)
            ?? throw new TemplateDormApi.Exceptions.BusinessException(404, "报修工单不存在", 404);
        await _identityService.EnsureOwnStudentIdAsync(accountId, ticket.StudentId ?? string.Empty, cancellationToken);

        var uploaded = new List<RepairAttachmentDto>();
        foreach (var file in request.Files)
        {
            FileUploadResultDto? stored = null;
            try
            {
                stored = await _fileStorageService.SaveAsync(file, cancellationToken);
                uploaded.Add(await _repository.AddAttachmentAsync(
                    ticketId,
                    stored.StorageRef,
                    file.FileName,
                    file.ContentType,
                    file.Length,
                    cancellationToken));
            }
            catch when (!cancellationToken.IsCancellationRequested)
            {
                if (stored is not null)
                {
                    await _fileStorageService.DeleteAsync(stored.StorageRef, CancellationToken.None);
                }
            }
        }

        if (uploaded.Count == 0)
        {
            throw new TemplateDormApi.Exceptions.BusinessException(400, "所有附件均上传失败");
        }

        return uploaded;
    }
}
