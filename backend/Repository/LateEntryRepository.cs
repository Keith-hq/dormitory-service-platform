using Microsoft.EntityFrameworkCore;
using TemplateDormApi.Data;
using TemplateDormApi.DTO;
using TemplateDormApi.Models;

namespace TemplateDormApi.Repository;

public sealed class LateEntryRepository : FrameworkRepositoryBase
{
    public LateEntryRepository(AppDbContext context) : base(context) { }

    public Task<string?> GetAdminIdAsync(int accountId, CancellationToken cancellationToken)
        => DbContext.UserAccounts.AsNoTracking()
            .Where(item => item.AccountId == accountId && item.AccountStatus == "正常")
            .Select(item => item.AdminId)
            .SingleOrDefaultAsync(cancellationToken);

    /// <summary>
    /// 查询学生当前所住楼栋的楼长工号，用于学生补充晚归说明后通知核对人。
    /// 收件人口径与楼栋保洁一致（RoleLevel='楼长'），避免把通知投给同楼的维修员/辅导员。
    /// 学生无在住床位（CheckOut_Date 为空）、房间未关联楼栋或该楼未配楼长时返回空集合，
    /// 调用方需据此告警——宿管端没有晚归列表接口，通知是学生说明的唯一送达通道。
    /// </summary>
    public async Task<IReadOnlyList<string>> GetBuildingDormAdminIdsAsync(
        string studentId,
        CancellationToken cancellationToken)
    {
        var roomId = await DbContext.BedAllocations.AsNoTracking()
            .Where(item => item.StudentId == studentId && item.CheckOutDate == null)
            .Select(item => item.RoomId)
            .FirstOrDefaultAsync(cancellationToken);
        if (roomId is null)
        {
            return Array.Empty<string>();
        }

        var buildingId = await DbContext.Rooms.AsNoTracking()
            .Where(room => room.RoomId == roomId.Value)
            .Select(room => room.BuildingId)
            .FirstOrDefaultAsync(cancellationToken);
        if (buildingId is null)
        {
            return Array.Empty<string>();
        }

        return await DbContext.Admins.AsNoTracking()
            .Where(admin => admin.BuildingId == buildingId.Value && admin.RoleLevel == "楼长")
            .OrderBy(admin => admin.AdminId)
            .Select(admin => admin.AdminId)
            .ToListAsync(cancellationToken);
    }

    public async Task<PagedResult<LateEntryDto>> GetStudentEntriesAsync(
        string studentId,
        LateEntryQueryDto query,
        CancellationToken cancellationToken)
    {
        var entryQuery = DbContext.LateEntries
            .AsNoTracking()
            .Where(item => item.StudentId == studentId);

        var total = await entryQuery.CountAsync(cancellationToken);
        var items = await entryQuery
            .OrderByDescending(item => item.ReturnTime)
            .ThenByDescending(item => item.RecordId)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(item => new LateEntryDto
            {
                RecordId = item.RecordId,
                StudentId = item.StudentId!,
                RecordTime = item.ReturnTime,
                Reason = item.Reason
            })
            .ToListAsync(cancellationToken);

        return new PagedResult<LateEntryDto>
        {
            Items = items,
            Total = total,
            Page = query.Page,
            PageSize = query.PageSize
        };
    }

    public Task<LateEntry?> FindByIdAsync(long recordId, CancellationToken cancellationToken)
        => DbContext.LateEntries.SingleOrDefaultAsync(
            item => item.RecordId == recordId,
            cancellationToken);

    public async Task<LateEntryDto> UpdateReasonAsync(
        LateEntry entry,
        string reason,
        CancellationToken cancellationToken)
    {
        entry.Reason = reason;
        await DbContext.SaveChangesAsync(cancellationToken);

        return new LateEntryDto
        {
            RecordId = entry.RecordId,
            StudentId = entry.StudentId!,
            RecordTime = entry.ReturnTime,
            Reason = entry.Reason
        };
    }

    public async Task<LateEntryDto> CreateAsync(
        CreateLateEntryRequest request,
        CancellationToken cancellationToken)
    {
        var studentExists = await DbContext.Students.AsNoTracking()
            .CountAsync(item => item.StudentId == request.StudentId, cancellationToken) > 0;
        if (!studentExists)
        {
            throw new TemplateDormApi.Exceptions.BusinessException(404, "学生不存在", 404);
        }

        // Reason 专属于「学生补充说明」：登记时不再写入宿管的现场说明。
        // 该列只有一列，若登记即占用，学生的 PUT 会把它整体覆盖且无法回溯；
        // 宿管的现场说明改由 LateEntryService 随登记通知投递给该生留档（通知行不可变）。
        var entry = new LateEntry
        {
            StudentId = request.StudentId,
            ReturnTime = request.RecordTime,
            Reason = null
        };
        DbContext.LateEntries.Add(entry);
        await DbContext.SaveChangesAsync(cancellationToken);
        return new LateEntryDto
        {
            RecordId = entry.RecordId,
            StudentId = entry.StudentId,
            RecordTime = entry.ReturnTime,
            Reason = entry.Reason
        };
    }
}
