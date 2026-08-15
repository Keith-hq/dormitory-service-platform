using Microsoft.EntityFrameworkCore;
using TemplateDormApi.Data;
using TemplateDormApi.Models;

namespace TemplateDormApi.Repository;

public class BedAllocationRepository : BaseRepository<BedAllocation>
{
    public BedAllocationRepository(AppDbContext context) : base(context) { }

    /// <summary>主键为 long 的重载（基类为 int 版；EF Find 对主键类型严格匹配，int 会抛 ArgumentException）</summary>
    public Task<BedAllocation?> GetByIdAsync(long id)
        => _dbSet.FindAsync(id).AsTask();

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
