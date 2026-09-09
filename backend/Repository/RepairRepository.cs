using Microsoft.EntityFrameworkCore;
using TemplateDormApi.Data;
using TemplateDormApi.DTO;
using TemplateDormApi.Models;
using TemplateDormApi.Exceptions;
using TemplateDormApi.Services;
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
            .Where(item => item.StudentId == studentId && !CleaningTags.IsCleaning(item.IssueDescription));

        var total = await ticketQuery.CountAsync(cancellationToken);
        var tickets = await ticketQuery
            .OrderByDescending(item => item.SubmitTime)
            .ThenByDescending(item => item.TicketId)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);

        // 四审 Oracle 实测教训：避开导航 Include（投影会拼影子列触发 ORA-00904），
        // 用三段独立查询手动挂载日志与附件，学生端可回看维修记录。
        var ticketIds = tickets.Select(item => item.TicketId).ToList();
        var logs = await DbContext.RepairLogs
            .AsNoTracking()
            .Where(log => log.TicketId.HasValue && ticketIds.Contains(log.TicketId.Value))
            .ToListAsync(cancellationToken);
        var attachments = await DbContext.RepairAttachments
            .AsNoTracking()
            .Where(att => ticketIds.Contains(att.TicketId))
            .ToListAsync(cancellationToken);

        foreach (var ticket in tickets)
        {
            ticket.Log = logs.FirstOrDefault(log => log.TicketId == ticket.TicketId);
            ticket.Attachments = attachments
                .Where(att => att.TicketId == ticket.TicketId)
                .OrderBy(att => att.CreateTime)
                .ToList();
        }

        var items = tickets.Select(ToDto).ToList();

        return new PagedResult<RepairTicketDto>
        {
            Items = items,
            Total = total,
            Page = query.Page,
            PageSize = query.PageSize
        };
    }

    public async Task<RepairTicket?> FindByIdAsync(long ticketId, CancellationToken cancellationToken)
    {
        // 四审真实 Oracle 实测（IT-C4-001 执行现场）：Include 导航会让 EF 在投影中
        // 追加影子列 "TicketId1"，Oracle provider 将其作为物理列拼入 SELECT →
        // ORA-00904（InMemory 单测无法暴露）。改为三段独立查询手动挂载，
        // 彻底避开导航 Include。
        var ticket = await DbContext.RepairTickets
            .SingleOrDefaultAsync(item => item.TicketId == ticketId, cancellationToken);
        if (ticket is null)
        {
            return null;
        }

        ticket.Log = await DbContext.RepairLogs
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.TicketId == ticketId, cancellationToken);

        ticket.Attachments = await DbContext.RepairAttachments
            .AsNoTracking()
            .Where(item => item.TicketId == ticketId)
            .ToListAsync(cancellationToken);
        return ticket;
    }

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
        ClaimTime = item.ClaimTime,
        Log = item.Log is null ? null : new RepairLogDto
        {
            AdminId = item.Log.AdminId,
            ProcessDescription = item.Log.ProcessDescription,
            RepairResult = item.Log.RepairResult,
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
