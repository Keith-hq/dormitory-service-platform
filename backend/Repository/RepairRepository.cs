using Microsoft.EntityFrameworkCore;
using TemplateDormApi.Data;
using TemplateDormApi.DTO;
using TemplateDormApi.Models;
using TemplateDormApi.Exceptions;
using Microsoft.AspNetCore.Http;

namespace TemplateDormApi.Repository;

public sealed class RepairRepository : FrameworkRepositoryBase
{
    public RepairRepository(AppDbContext context) : base(context) { }

    public Task<string?> GetStudentIdAsync(int accountId, CancellationToken cancellationToken)
        => DbContext.UserAccounts.AsNoTracking()
            .Where(item => item.AccountId == accountId && item.AccountStatus == "正常")
            .Select(item => item.StudentId)
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<RepairTicketDto> CreateAsync(
        string studentId,
        SubmitRepairTicketRequest request,
        CancellationToken cancellationToken)
    {
        var roomId = request.RoomId ?? await DbContext.BedAllocations
            .AsNoTracking()
            .Where(item => item.StudentId == studentId && item.CheckOutDate == null && item.RoomId != null)
            .OrderByDescending(item => item.CheckInDate)
            .Select(item => item.RoomId)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new BusinessException(409, "当前没有有效住宿，且未指定报修房间", StatusCodes.Status409Conflict);

        var roomNum = await DbContext.Rooms.AsNoTracking()
            .CountAsync(item => item.RoomId == roomId, cancellationToken);
        if (roomNum == 0)
        {
            throw new BusinessException(404, "报修房间不存在", StatusCodes.Status404NotFound);
        }

        var ticket = new RepairTicket
        {
            StudentId = studentId,
            RoomId = roomId,
            IssueDescription = request.Description.Trim(),
            SubmitTime = DateTime.Now,
            Status = "待处理",
            SlaLevel = request.Urgency
        };
        DbContext.RepairTickets.Add(ticket);
        await DbContext.SaveChangesAsync(cancellationToken);
        return ToDto(ticket);
    }

    /// <summary>
    /// 宿管侧「损坏资产转报修」（DORM-16）入口：创建一条资产关联的报修工单。
    /// Student_ID 为空（宿管发起，非学生报修）、Room_ID 取资产所属房间、初始状态
    /// 待处理。仅把工单加入变更跟踪，不 SaveChanges——由 AssetService 与
    /// D_Asset_Repair / D_Asset_Warning 写入置于同一事务统一提交。
    /// </summary>
    public RepairTicket CreateAssetTicket(int? roomId, string description)
    {
        var ticket = new RepairTicket
        {
            StudentId = null,
            RoomId = roomId,
            IssueDescription = description.Trim(),
            SubmitTime = DateTime.Now,
            Status = "待处理",
            SlaLevel = "普通"
        };
        DbContext.RepairTickets.Add(ticket);
        return ticket;
    }

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

    public Task<RepairTicket?> FindByIdAsync(long ticketId, CancellationToken cancellationToken)
        => DbContext.RepairTickets
            .Include(item => item.Log)
            .Include(item => item.Attachments)
            .SingleOrDefaultAsync(item => item.TicketId == ticketId, cancellationToken);

    public async Task<RepairTicketDto> CancelAsync(RepairTicket ticket, CancellationToken cancellationToken)
    {
        ticket.Status = "已撤销";
        await DbContext.SaveChangesAsync(cancellationToken);
        return ToDto(ticket);
    }

    public async Task<RepairAttachmentDto> AddAttachmentAsync(
        long ticketId,
        string storageRef,
        string originalName,
        string contentType,
        long fileSize,
        CancellationToken cancellationToken)
    {
        var attachment = new RepairAttachment
        {
            TicketId = ticketId,
            StorageRef = storageRef,
            OriginalName = originalName,
            ContentType = contentType,
            FileSize = fileSize,
            CreateTime = DateTime.Now
        };
        DbContext.RepairAttachments.Add(attachment);
        await DbContext.SaveChangesAsync(cancellationToken);
        return ToDto(attachment);
    }

    public static RepairTicketDto ToDto(RepairTicket item) => new()
    {
        TicketId = item.TicketId,
        StudentId = item.StudentId ?? string.Empty,
        RoomId = item.RoomId ?? 0,
        Description = item.IssueDescription,
        SubmitTime = item.SubmitTime,
        Status = item.Status ?? string.Empty,
        SlaLevel = item.SlaLevel,
        Deadline = item.Deadline,
        AssignedTo = item.AssignedTo,
        Log = item.Log is null ? null : new RepairLogDto
        {
            AdminId = item.Log.AdminId,
            ProcessDescription = item.Log.ProcessDescription,
            ResolveTime = item.Log.ResolveTime
        },
        Attachments = item.Attachments.OrderBy(item => item.CreateTime).Select(ToDto).ToList()
    };

    private static RepairAttachmentDto ToDto(RepairAttachment item) => new()
    {
        AttachmentId = item.AttachmentId,
        TicketId = item.TicketId,
        StorageRef = item.StorageRef,
        OriginalName = item.OriginalName ?? string.Empty,
        ContentType = item.ContentType ?? string.Empty,
        FileSize = item.FileSize ?? 0,
        CreateTime = item.CreateTime
    };
}
