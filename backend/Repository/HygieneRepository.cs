using Microsoft.EntityFrameworkCore;
using TemplateDormApi.Data;
using TemplateDormApi.DTO;
using TemplateDormApi.Models;

namespace TemplateDormApi.Repository;

public sealed class HygieneRepository : FrameworkRepositoryBase
{
    public HygieneRepository(AppDbContext context) : base(context) { }

    public async Task<long?> GetCurrentRoomIdAsync(
        int accountId,
        CancellationToken cancellationToken)
    {
        var studentId = await DbContext.UserAccounts
            .AsNoTracking()
            .Where(item => item.AccountId == accountId && item.AccountStatus == "正常")
            .Select(item => item.StudentId)
            .SingleOrDefaultAsync(cancellationToken);

        if (studentId is null)
        {
            return null;
        }

        return await DbContext.BedAllocations
            .AsNoTracking()
            .Where(item =>
                item.StudentId == studentId &&
                item.CheckOutDate == null &&
                item.RoomId != null)
            .OrderByDescending(item => item.CheckInDate)
            .ThenByDescending(item => item.AllocationId)
            .Select(item => item.RoomId)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<HygieneRecordDto>> GetRoomRecordsAsync(
        long roomId,
        CancellationToken cancellationToken)
    {
        return await DbContext.HygieneRecords
            .AsNoTracking()
            .Where(item => item.RoomId == roomId)
            .OrderByDescending(item => item.CheckDate)
            .ThenByDescending(item => item.RecordId)
            .Select(item => new HygieneRecordDto
            {
                RecordId = item.RecordId,
                RoomId = item.RoomId ?? 0,
                CheckDate = item.CheckDate,
                Score = item.Score,
                InspectorId = item.InspectorId,
                Comment = item.Comment == null ? null : item.Comment.CommentText
            })
            .ToListAsync(cancellationToken);
    }

    public Task<HygieneRecordDto> CreateAsync(
        CreateHygieneRecordRequest request,
        CancellationToken cancellationToken)
        => PendingAsync<HygieneRecordDto>(
            "DORM-32",
            "卫生记录和评语主键生成方案待确认",
            cancellationToken);

    public Task<HygieneRecord?> FindByIdAsync(long recordId, CancellationToken cancellationToken)
        => DbContext.HygieneRecords
            .Include(item => item.Comment)
            .SingleOrDefaultAsync(item => item.RecordId == recordId, cancellationToken);

    public async Task<HygieneRecordDto> UpdateAsync(
        HygieneRecord record,
        UpdateHygieneRecordRequest request,
        CancellationToken cancellationToken)
    {
        record.Score = request.Score;
        if (request.Comment is not null)
        {
            if (record.Comment is null)
            {
                record.Comment = new HygieneComment
                {
                    RecordId = record.RecordId,
                    CommentText = request.Comment
                };
            }
            else
            {
                record.Comment.CommentText = request.Comment;
            }
        }

        await DbContext.SaveChangesAsync(cancellationToken);
        return new HygieneRecordDto
        {
            RecordId = record.RecordId,
            RoomId = record.RoomId ?? 0,
            CheckDate = record.CheckDate,
            Score = record.Score,
            InspectorId = record.InspectorId,
            Comment = record.Comment?.CommentText
        };
    }

    public Task<IReadOnlyList<HygieneRankingDto>> GetRankingsAsync(
        HygieneRankingQueryDto query,
        CancellationToken cancellationToken)
        => PendingAsync<IReadOnlyList<HygieneRankingDto>>(
            "DORM-34",
            "月度排名统计口径待确认",
            cancellationToken);
}
