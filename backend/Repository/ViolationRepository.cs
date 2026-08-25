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
    /// 落库 D_Violation_Record；注意：违规登记不触发信用扣分（与信用扣分解耦）。
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
            RecordBy = string.IsNullOrWhiteSpace(recordBy) ? null : recordBy
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
            RecordBy = record.RecordBy
        };
    }

    public Task<PagedResult<ViolationDto>> GetPagedAsync(
        ViolationQueryDto query,
        CancellationToken cancellationToken)
        => PendingAsync<PagedResult<ViolationDto>>(
            "VIOL-02",
            "按楼栋筛选所需关联和返回字段口径待确认",
            cancellationToken);
}
