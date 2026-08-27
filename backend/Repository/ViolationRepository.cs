using System.Data;
using Microsoft.EntityFrameworkCore;
using TemplateDormApi.Data;
using TemplateDormApi.DTO;
using TemplateDormApi.Exceptions;
using TemplateDormApi.Models;

namespace TemplateDormApi.Repository;

public sealed class ViolationRepository : FrameworkRepositoryBase
{
    public ViolationRepository(AppDbContext context) : base(context) { }

    /// <summary>
    /// VIOL-01 登记违规（迁移 037 落地实现，解除 501 占位）。
    /// 落库 D_Violation_Record；信用扣分由 ViolationService 在创建后按类型触发
    /// （违章电器 -10、其余 -5，EventKey 幂等，失败时由服务层回删补偿）。
    /// 主键取 SEQ_D_VIOLATION；序列缺失时回退 MAX+1。
    /// </summary>
    public async Task<ViolationDto> CreateAsync(
        CreateViolationRequest request,
        string? recordBy,
        CancellationToken cancellationToken)
    {
        var studentExists = await DbContext.Students.AsNoTracking()
            .CountAsync(item => item.StudentId == request.StudentId, cancellationToken) > 0;
        if (!studentExists)
        {
            throw new BusinessException(404, "学生不存在", 404);
        }

        // 学生当前房间：取最近未退宿的床位分配（D_Student 无房间列）
        var roomId = await DbContext.BedAllocations.AsNoTracking()
            .Where(b => b.StudentId == request.StudentId
                && (b.CheckOutDate == null || b.CheckOutDate >= DateTime.Today))
            .OrderByDescending(b => b.CheckInDate)
            .Select(b => (int?)b.RoomId)
            .FirstOrDefaultAsync(cancellationToken);

        // 主键取序列：NEXTVAL 不能经 EF SqlQueryRaw（会被包进子查询 → ORA-02287），
        // 走 ADO.NET 直连取数；序列缺失时回退 MAX+1。
        var conn = DbContext.Database.GetDbConnection();
        var wasOpen = conn.State == ConnectionState.Open;
        if (!wasOpen) await conn.OpenAsync(cancellationToken);
        int nextId;
        try
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT SEQ_D_VIOLATION.NEXTVAL FROM DUAL";
            nextId = Convert.ToInt32(await cmd.ExecuteScalarAsync(cancellationToken));
        }
        catch (Oracle.ManagedDataAccess.Client.OracleException ex) when (ex.Number == 2289)
        {
            nextId = (await DbContext.ViolationRecords.MaxAsync(r => (int?)r.RecordId, cancellationToken) ?? 0) + 1;
        }
        finally
        {
            if (!wasOpen) await conn.CloseAsync();
        }

        var record = new ViolationRecord
        {
            RecordId = nextId,
            StudentId = request.StudentId,
            RoomId = roomId,
            VioType = request.Type,
            VioDate = DateTime.Now,
            Penalty = null,
            Detail = string.IsNullOrWhiteSpace(request.Detail) ? null : request.Detail.Trim(),
            RecordBy = string.IsNullOrWhiteSpace(recordBy) ? null : recordBy,
            Status = "有效"
        };
        DbContext.ViolationRecords.Add(record);
        await DbContext.SaveChangesAsync(cancellationToken);

        return new ViolationDto
        {
            ViolationId = record.RecordId,
            StudentId = record.StudentId ?? string.Empty,
            Type = record.VioType,
            Detail = record.Detail,
            RecordTime = record.VioDate,
            RecordBy = record.RecordBy,
            Status = record.Status
        };
    }

    /// <summary>按主键标记违规为「已撤销」（信用申诉通过时联动，幂等）。</summary>
    public async Task<bool> MarkRevokedAsync(int violationId, CancellationToken cancellationToken)
    {
        var record = await DbContext.ViolationRecords
            .FirstOrDefaultAsync(item => item.RecordId == violationId, cancellationToken);
        if (record is null || record.Status == "已撤销")
        {
            return false;
        }

        record.Status = "已撤销";
        await DbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    /// <summary>按主键查询违规记录（补偿回删前重查用）。</summary>
    public async Task<ViolationRecord?> FindByIdAsync(int id, CancellationToken cancellationToken)
        => await DbContext.ViolationRecords.FirstOrDefaultAsync(
            item => item.RecordId == id,
            cancellationToken);

    /// <summary>删除违规记录（补偿回删用，仅删记录不联动信用分）。</summary>
    public Task DeleteAsync(ViolationRecord record, CancellationToken cancellationToken)
    {
        DbContext.ViolationRecords.Remove(record);
        return DbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<PagedResult<ViolationDto>> GetPagedAsync(
        ViolationQueryDto query,
        CancellationToken cancellationToken)
    {
        query.Page = Math.Max(query.Page, 1);
        query.PageSize = query.PageSize is < 1 or > 100 ? 10 : query.PageSize;

        var itemsQuery = DbContext.ViolationRecords.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.StudentId))
        {
            itemsQuery = itemsQuery.Where(item => item.StudentId == query.StudentId);
        }

        // 楼栋筛选：违规时的房间（Violation.RoomId）→ 楼栋（Room.BuildingId）
        if (query.BuildingId.HasValue)
        {
            var buildingId = (int)query.BuildingId.Value;
            itemsQuery = itemsQuery.Where(
                item => DbContext.Rooms.Any(
                    room => room.RoomId == item.RoomId && room.BuildingId == buildingId));
        }

        var total = await itemsQuery.CountAsync(cancellationToken);
        var items = await itemsQuery
            .OrderByDescending(item => item.VioDate)
            .ThenByDescending(item => item.RecordId)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(item => new ViolationDto
            {
                ViolationId = item.RecordId,
                StudentId = item.StudentId ?? string.Empty,
                Type = item.VioType,
                Detail = item.Detail,
                RecordTime = item.VioDate,
                RecordBy = item.RecordBy,
                Status = item.Status
            })
            .ToListAsync(cancellationToken);

        return new PagedResult<ViolationDto>
        {
            Items = items,
            Total = total,
            Page = query.Page,
            PageSize = query.PageSize
        };
    }
}
