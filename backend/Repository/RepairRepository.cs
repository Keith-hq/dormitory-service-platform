using Microsoft.EntityFrameworkCore;
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

    public async Task<PagedResult<RepairTicketDto>> GetStudentTicketsAsync(
        string studentId,
        RepairTicketQueryDto query,
        CancellationToken cancellationToken)
    {
        var ticketQuery = DbContext.RepairTickets
            .AsNoTracking()
            .Where(item => item.StudentId == studentId);

        var total = await ticketQuery.CountAsync(cancellationToken);
        var items = await ticketQuery
            .OrderByDescending(item => item.SubmitTime)
            .ThenByDescending(item => item.TicketId)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(item => new RepairTicketDto
            {
                TicketId = item.TicketId,
                StudentId = item.StudentId!,
                RoomId = item.RoomId ?? 0,
                Description = item.IssueDescription,
                SubmitTime = item.SubmitTime,
                Status = item.Status ?? string.Empty,
                SlaLevel = item.SlaLevel,
                Deadline = item.Deadline,
                AssignedTo = item.AssignedTo
            })
            .ToListAsync(cancellationToken);

        return new PagedResult<RepairTicketDto>
        {
            Items = items,
            Total = total,
            Page = query.Page,
            PageSize = query.PageSize
        };
    }

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
