using Microsoft.EntityFrameworkCore;
using TemplateDormApi.Data;
using TemplateDormApi.DTO;
using TemplateDormApi.Models;

namespace TemplateDormApi.Repository;

public sealed class LateEntryRepository : FrameworkRepositoryBase
{
    public LateEntryRepository(AppDbContext context) : base(context) { }

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

    public Task<LateEntryDto> CreateAsync(
        CreateLateEntryRequest request,
        CancellationToken cancellationToken)
        => PendingAsync<LateEntryDto>(
            "DORM-31",
            "D_LATE_ENTRY 新增记录的主键生成方案待确认",
            cancellationToken);
}
