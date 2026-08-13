using Microsoft.EntityFrameworkCore;
using TemplateDormApi.Data;
using TemplateDormApi.Models;

namespace TemplateDormApi.Repository;

public class BedAllocationRepository : BaseRepository<BedAllocation>
{
    public BedAllocationRepository(AppDbContext context) : base(context) { }

    /// <summary>主键生成：MAX+1（表无序列，DDL 冻结不改；并发撞号由 DbSaveRetry 重试兜底）</summary>
    public async Task<long> NextAllocationIdAsync()
        => await _dbSet.AnyAsync() ? await _dbSet.MaxAsync(a => a.AllocationId) + 1L : 1L;

    /// <summary>某房间某床位当前在住分配（UK_D_BED_ALLOC_ACTIVE 的代码侧快查，数据库约束为最终兜底）</summary>
    public Task<BedAllocation?> GetActiveByRoomBedAsync(int roomId, int bedNo)
        => _dbSet.FirstOrDefaultAsync(a => a.RoomId == roomId && a.BedNo == bedNo && a.CheckOutDate == null);

    /// <summary>学生当前在住分配（一人一床业务规则快查）</summary>
    public Task<BedAllocation?> GetActiveByStudentAsync(string studentId)
        => _dbSet.FirstOrDefaultAsync(a => a.StudentId == studentId && a.CheckOutDate == null);

    /// <summary>某房间当前住户列表（按床位号排序）</summary>
    public Task<List<BedAllocation>> GetActiveByRoomAsync(int roomId)
        => _dbSet
            .Where(a => a.RoomId == roomId && a.CheckOutDate == null)
            .OrderBy(a => a.BedNo)
            .ToListAsync();
}
