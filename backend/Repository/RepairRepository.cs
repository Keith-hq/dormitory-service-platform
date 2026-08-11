using TemplateDormApi.Data;
using TemplateDormApi.DTO;

namespace TemplateDormApi.Repository;

public sealed class RepairRepository : FrameworkRepositoryBase
{
    public RepairRepository(AppDbContext context) : base(context) { }

    public Task<RepairTicketDto> CreateAsync(
        SubmitRepairTicketRequest request,
        CancellationToken cancellationToken)
        => PendingAsync<RepairTicketDto>(
            "STU-08",
            "D_REPAIR_TICKET 缺少 CATEGORY 字段，且新增记录主键生成方案待确认",
            cancellationToken);

    public Task<PagedResult<RepairTicketDto>> GetStudentTicketsAsync(
        string studentId,
        RepairTicketQueryDto query,
        CancellationToken cancellationToken)
        => PendingAsync<PagedResult<RepairTicketDto>>(
            "STU-09",
            "工单分页及学生数据权限查询待实现",
            cancellationToken);

    public Task<RepairTicketDto> GetByIdAsync(long ticketId, CancellationToken cancellationToken)
        => PendingAsync<RepairTicketDto>(
            "STU-10",
            "工单详情与处理日志组合口径待确认",
            cancellationToken);

    public Task<RepairTicketDto> CancelAsync(long ticketId, CancellationToken cancellationToken)
        => PendingAsync<RepairTicketDto>(
            "STU-11",
            "撤销状态及十分钟 SLA 规则待业务实现",
            cancellationToken);

    public Task<IReadOnlyList<RepairAttachmentDto>> AddAttachmentsAsync(
        long ticketId,
        UploadRepairAttachmentsRequest request,
        CancellationToken cancellationToken)
        => PendingAsync<IReadOnlyList<RepairAttachmentDto>>(
            "STU-12",
            "附件记录主键生成方案待确认",
            cancellationToken);
}
