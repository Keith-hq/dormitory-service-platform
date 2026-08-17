using Microsoft.EntityFrameworkCore;
using TemplateDormApi.Data;
using TemplateDormApi.Models;

namespace TemplateDormApi.Repository;

/// <summary>
/// 房间投票 Repository（继承 BaseRepository&lt;RoomVote&gt; 获得基础 CRUD）。
/// 投票响应表 D_Room_Vote_Response 与投票同属一个聚合，操作也收敛在此。
/// </summary>
public class VoteRepository : BaseRepository<RoomVote>
{
    public VoteRepository(AppDbContext context) : base(context) { }

    /// <summary>查询某房间的投票列表（对齐契约 GET /rooms/{roomId}/votes，不分页）。</summary>
    public async Task<List<RoomVote>> GetByRoomAsync(int roomId)
        => await _dbSet.AsNoTracking()
            .Where(v => v.RoomId == roomId)
            .OrderByDescending(v => v.VoteId)
            .ToListAsync();

    /// <summary>查询学生是否已对某投票投过票（一人一票前置校验）。</summary>
    /// <remarks>四审真实 Oracle 实测：AnyAsync 被翻译为 CASE WHEN EXISTS(...) THEN True
    /// ELSE False，Oracle 21c 无布尔字面量 → ORA-00904。改用 CountAsync（翻译为 COUNT(*)）。</remarks>
    public async Task<bool> HasRespondedAsync(int voteId, string studentId)
        => await _context.Set<RoomVoteResponse>()
            .CountAsync(r => r.VoteId == voteId && r.StudentId == studentId) > 0;

    /// <summary>判断学生是否为投票房间的在住成员（D_Bed_Allocation 中 CheckOut_Date 为空）。
    /// 对齐 IT-C8-002 通过判定②：非本房间学生不可投。</summary>
    public async Task<bool> IsRoomMemberAsync(int roomId, string studentId)
        => await _context.Set<BedAllocation>()
            .CountAsync(b => b.StudentId == studentId && b.RoomId == roomId && b.CheckOutDate == null) > 0;

    /// <summary>写入一条投票响应。复合主键（Vote_ID + Student_ID）是一人一票的最终兜底。</summary>
    public async Task AddResponseAsync(RoomVoteResponse response)
    {
        await _context.Set<RoomVoteResponse>().AddAsync(response);
        await _context.SaveChangesAsync();
    }

    /// <summary>统计某投票的同意/不同意票数。</summary>
    public async Task<(int Agree, int Disagree)> CountChoicesAsync(int voteId)
    {
        var choices = await _context.Set<RoomVoteResponse>()
            .AsNoTracking()
            .Where(r => r.VoteId == voteId)
            .Select(r => r.Choice)
            .ToListAsync();

        return (choices.Count(c => c == "同意"), choices.Count(c => c == "不同意"));
    }
}
