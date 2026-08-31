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

    public Task<string?> GetAdminIdAsync(int accountId, CancellationToken cancellationToken)
        => DbContext.UserAccounts.AsNoTracking()
            .Where(item => item.AccountId == accountId && item.AccountStatus == "正常")
            .Select(item => item.AdminId)
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<long?> GetAccessibleBuildingIdAsync(
        int accountId,
        bool isDormAdmin,
        CancellationToken cancellationToken)
    {
        if (isDormAdmin)
        {
            var adminId = await GetAdminIdAsync(accountId, cancellationToken);
            return adminId is null
                ? null
                : await DbContext.Admins.AsNoTracking()
                    .Where(item => item.AdminId == adminId)
                    .Select(item => item.BuildingId)
                    .SingleOrDefaultAsync(cancellationToken);
        }

        var roomId = await GetCurrentRoomIdAsync(accountId, cancellationToken);
        return roomId.HasValue
            ? await DbContext.Rooms.AsNoTracking()
                .Where(item => item.RoomId == roomId.Value)
                .Select(item => (long?)item.BuildingId)
                .SingleOrDefaultAsync(cancellationToken)
            : null;
    }

    public async Task<IReadOnlyList<HygieneRecordDto>> GetRoomRecordsAsync(
        int roomId,
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
                Comment = item.CommentText
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<HygieneRecordDto> CreateAsync(
        string adminId,
        CreateHygieneRecordRequest request,
        CancellationToken cancellationToken)
    {
        var roomExists = await DbContext.Rooms.AsNoTracking()
            .CountAsync(item => item.RoomId == request.RoomId, cancellationToken) > 0;
        if (!roomExists)
        {
            throw new TemplateDormApi.Exceptions.BusinessException(404, "房间不存在", 404);
        }

        var record = new HygieneRecord
        {
            RoomId = request.RoomId,
            CheckDate = DateTime.Now,
            Score = request.Score,
            InspectorId = adminId
        };
        // 041 起评语列并入 D_Hygiene_Record 本体，直接赋值 CommentText
        if (!string.IsNullOrWhiteSpace(request.Comment))
        {
            record.CommentText = request.Comment.Trim();
        }

        DbContext.HygieneRecords.Add(record);
        await DbContext.SaveChangesAsync(cancellationToken);
        return new HygieneRecordDto
        {
            RecordId = record.RecordId,
            RoomId = request.RoomId,
            CheckDate = record.CheckDate,
            Score = record.Score,
            InspectorId = adminId,
            Comment = record.CommentText
        };
    }

    public Task<HygieneRecord?> FindByIdAsync(long recordId, CancellationToken cancellationToken)
        => DbContext.HygieneRecords
            .SingleOrDefaultAsync(item => item.RecordId == recordId, cancellationToken);

    public async Task<HygieneRecordDto> UpdateAsync(
        HygieneRecord record,
        UpdateHygieneRecordRequest request,
        CancellationToken cancellationToken)
    {
        record.Score = request.Score;
        if (request.Comment is not null)
        {
            record.CommentText = request.Comment;
        }

        await DbContext.SaveChangesAsync(cancellationToken);
        return new HygieneRecordDto
        {
            RecordId = record.RecordId,
            RoomId = record.RoomId ?? 0,
            CheckDate = record.CheckDate,
            Score = record.Score,
            InspectorId = record.InspectorId,
            Comment = record.CommentText
        };
    }

    public async Task<IReadOnlyList<HygieneRankingDto>> GetRankingsAsync(
        HygieneRankingQueryDto query,
        long buildingId,
        CancellationToken cancellationToken)
    {
        var month = ParseMonth(query.YearMonth);
        var nextMonth = month.AddMonths(1);

        // 月榜取每间房"最新一次"打分的分数（不平均）：先按时间倒序，再按房间分组取第一条。
        // 修正历史记录后，月榜即等于最新一次打分，不再被同月旧打分稀释。
        var records = await DbContext.HygieneRecords.AsNoTracking()
            .Where(item =>
                item.RoomId != null &&
                item.CheckDate >= month &&
                item.CheckDate < nextMonth &&
                DbContext.Rooms.Any(room => room.RoomId == item.RoomId && room.BuildingId == buildingId))
            .OrderByDescending(item => item.CheckDate)
            .ThenByDescending(item => item.RecordId)
            .ToListAsync(cancellationToken);

        var latestScores = records
            .GroupBy(item => item.RoomId!.Value)
            .Select(group => new
            {
                RoomId = group.Key,
                LatestScore = group.First().Score
            })
            .OrderByDescending(item => item.LatestScore)
            .ThenBy(item => item.RoomId)
            .ToList();

        var result = new List<HygieneRankingDto>();
        decimal? previousScore = null;
        var rank = 0;
        foreach (var item in latestScores)
        {
            if (previousScore != item.LatestScore)
            {
                rank++;
                previousScore = item.LatestScore;
            }
            if (item.LatestScore >= 90m)
            {
                result.Add(new HygieneRankingDto
                {
                    RoomId = item.RoomId,
                    AverageScore = Math.Round(item.LatestScore, 1),
                    Rank = rank
                });
            }
        }

        return result;
    }

    private static DateTime ParseMonth(string? yearMonth)
    {
        if (string.IsNullOrWhiteSpace(yearMonth))
        {
            var now = DateTime.Now;
            return new DateTime(now.Year, now.Month, 1);
        }

        return DateTime.ParseExact(yearMonth, "yyyy-MM", System.Globalization.CultureInfo.InvariantCulture);
    }
}
