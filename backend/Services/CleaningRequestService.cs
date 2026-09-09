using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using TemplateDormApi.Data;
using TemplateDormApi.DTO;
using TemplateDormApi.Exceptions;
using TemplateDormApi.Models;

namespace TemplateDormApi.Services;

public interface ICleaningRequestService
{
    Task<PagedResult<CleaningRequestDto>> GetListAsync(int page, int pageSize, CancellationToken ct);
    Task<IReadOnlyList<CleaningRequestDto>> GetMineAsync(string studentId, CancellationToken ct);
    Task<CleaningRequestDto> ApplyStudentAsync(string studentId, string? reason, CancellationToken ct);
    Task<CleaningRequestDto> CompleteAsync(long ticketId, CancellationToken ct);
    Task GenerateBuildingWeeklyAsync(CancellationToken ct);
}

/// <summary>
/// 保洁请求服务（借道 D_Repair_Ticket，Issue_Desc 前缀区分，见 CleaningTags）。
/// 宿舍申请 / 楼栋整体保洁 / 设施触发 三类并存；不走报修 SLA/派单链路。
/// </summary>
public sealed class CleaningRequestService : ICleaningRequestService
{
    private readonly AppDbContext _context;

    public CleaningRequestService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<CleaningRequestDto> ApplyStudentAsync(
        string studentId, string? reason, CancellationToken ct)
    {
        // 需当前在住房间
        var activeRoomId = await _context.BedAllocations.AsNoTracking()
            .Where(a => a.StudentId == studentId && a.CheckOutDate == null && a.RoomId != null)
            .OrderByDescending(a => a.CheckInDate)
            .Select(a => a.RoomId)
            .FirstOrDefaultAsync(ct);

        if (activeRoomId == null)
        {
            throw new BusinessException(409, "当前无在住房间，无法申请保洁（退宿后请先由宿管重新分配住宿）", 409);
        }

        // 同房待处理去重
        var pendingSameRoom = await _context.RepairTickets.AsNoTracking()
            .CountAsync(t =>
                t.RoomId == activeRoomId &&
                t.StudentId == studentId &&
                t.Status == "待处理" &&
                t.IssueDescription.StartsWith(CleaningTags.DormPrefix), ct) > 0;

        if (pendingSameRoom)
        {
            throw new BusinessException(409, "该宿舍已有待处理的保洁申请，请稍后再试", 409);
        }

        var description = $"{CleaningTags.DormPrefix}{(string.IsNullOrWhiteSpace(reason) ? "宿舍请求保洁" : reason.Trim())}";
        var ticket = new RepairTicket
        {
            StudentId = studentId,
            RoomId = activeRoomId,
            IssueDescription = description,
            SubmitTime = DateTime.Now,
            Status = "待处理",
            SlaLevel = "普通"
        };

        _context.RepairTickets.Add(ticket);
        await _context.SaveChangesAsync(ct);
        return await ProjectSingleAsync(ticket.TicketId, ct);
    }

    public async Task<CleaningRequestDto> CompleteAsync(long ticketId, CancellationToken ct)
    {
        var ticket = await _context.RepairTickets
            .FirstOrDefaultAsync(t => t.TicketId == ticketId, ct)
            ?? throw new BusinessException(404, "保洁任务不存在", StatusCodes.Status404NotFound);

        if (!CleaningTags.IsCleaning(ticket.IssueDescription))
        {
            throw new BusinessException(400, "该工单不是保洁任务", StatusCodes.Status400BadRequest);
        }

        if (ticket.Status == "已完成")
        {
            return await ProjectSingleAsync(ticket.TicketId, ct);
        }

        if (ticket.Status != "待处理")
        {
            throw new BusinessException(409, "只有「待处理」状态的保洁任务才能完成", StatusCodes.Status409Conflict);
        }

        ticket.Status = "已完成";
        await _context.SaveChangesAsync(ct);
        return await ProjectSingleAsync(ticket.TicketId, ct);
    }

    public async Task<PagedResult<CleaningRequestDto>> GetListAsync(
        int page, int pageSize, CancellationToken ct)
    {
        var query = BuildCleanProjection()
            .Where(t => t.IssueDescription.StartsWith(CleaningTags.SearchPrefix));

        var total = await query.CountAsync(ct);
        var pageTickets = await query
            .OrderByDescending(t => t.SubmitTime)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var items = new List<CleaningRequestDto>();
        foreach (var ticket in pageTickets)
        {
            items.Add(await EnrichAsync(ticket, ct));
        }

        return new PagedResult<CleaningRequestDto>
        {
            Items = items,
            Total = total,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<IReadOnlyList<CleaningRequestDto>> GetMineAsync(string studentId, CancellationToken ct)
    {
        var rows = await BuildCleanProjection()
            .Where(t => t.StudentId == studentId && t.IssueDescription.StartsWith(CleaningTags.DormPrefix))
            .OrderByDescending(t => t.SubmitTime)
            .ToListAsync(ct);
        var list = new List<CleaningRequestDto>();
        foreach (var ticket in rows)
        {
            list.Add(await EnrichAsync(ticket, ct));
        }
        return list;
    }

    public async Task GenerateBuildingWeeklyAsync(CancellationToken ct)
    {
        // 按“楼长负责的楼栋”每周生成楼栋整体保洁
        var admins = await _context.Admins.AsNoTracking()
            .Where(a => a.RoleLevel == "楼长" && a.BuildingId != null)
            .GroupBy(a => a.BuildingId!.Value)
            .Select(g => new { BuildingId = g.Key, AdminId = g.Min(a => a.AdminId) })
            .ToListAsync(ct);

        foreach (var item in admins)
        {
            _context.RepairTickets.Add(new RepairTicket
            {
                StudentId = null,
                RoomId = null,
                AssignedTo = item.AdminId,
                IssueDescription = $"{CleaningTags.BuildingPrefix}{item.BuildingId}",
                SubmitTime = DateTime.Now,
                Status = "待处理",
                SlaLevel = "普通"
            });
        }

        if (admins.Count > 0)
        {
            await _context.SaveChangesAsync(ct);
        }
    }

    // ---- projection helpers ----

    private IQueryable<RepairTicket> BuildCleanProjection()
        => _context.RepairTickets.AsNoTracking();

    private async Task<CleaningRequestDto> ProjectSingleAsync(long ticketId, CancellationToken ct)
    {
        var rows = await BuildCleanProjection()
            .Where(t => t.TicketId == ticketId)
            .ToListAsync(ct);
        var ticket = rows.FirstOrDefault()
            ?? throw new BusinessException(404, "保洁任务不存在", StatusCodes.Status404NotFound);
        return await EnrichAsync(ticket, ct);
    }

    private async Task<CleaningRequestDto> EnrichAsync(RepairTicket ticket, CancellationToken ct)
    {
        var room = ticket.RoomId.HasValue
            ? await _context.Rooms.AsNoTracking().FirstOrDefaultAsync(r => r.RoomId == ticket.RoomId, ct)
            : null;
        var building = room?.BuildingId.HasValue == true
            ? await _context.Buildings.AsNoTracking().FirstOrDefaultAsync(b => b.BuildingId == room!.BuildingId, ct)
            : null;
        var student = ticket.StudentId != null
            ? await _context.Students.AsNoTracking().FirstOrDefaultAsync(s => s.StudentId == ticket.StudentId, ct)
            : null;
        var assignee = ticket.AssignedTo != null
            ? await _context.Admins.AsNoTracking().FirstOrDefaultAsync(a => a.AdminId == ticket.AssignedTo, ct)
            : null;
        if (building == null && assignee?.BuildingId.HasValue == true)
        {
            building = await _context.Buildings.AsNoTracking()
                .FirstOrDefaultAsync(b => b.BuildingId == assignee.BuildingId, ct);
        }

        var dto = ToDto(ticket);
        dto.RoomNumber = room?.RoomNumber;
        dto.BuildingId = (room?.BuildingId) ?? (assignee?.BuildingId);
        dto.BuildingName = building?.BuildingName;
        dto.RequesterName = student?.Name;
        dto.AssigneeName = assignee?.AdminName;
        return dto;
    }

    private static CleaningRequestDto ToDto(RepairTicket ticket)
    {
        var dorm = ticket.IssueDescription.StartsWith(CleaningTags.DormPrefix, System.StringComparison.Ordinal);
        var buildingCleaning = ticket.IssueDescription.StartsWith(CleaningTags.BuildingPrefix, System.StringComparison.Ordinal);
        var source = dorm ? "宿舍申请" : buildingCleaning ? "楼栋整体" : "设施触发";
        return new CleaningRequestDto
        {
            TaskId = ticket.TicketId,
            SourceType = source,
            Description = ticket.IssueDescription,
            Status = ticket.Status ?? "待处理",
            RoomId = ticket.RoomId,
            RequesterStudentId = ticket.StudentId,
            AssigneeAdminId = ticket.AssignedTo,
            CreateTime = ticket.SubmitTime
        };
    }
}
