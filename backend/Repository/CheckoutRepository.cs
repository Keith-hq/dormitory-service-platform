using Microsoft.EntityFrameworkCore;
using TemplateDormApi.Data;
using TemplateDormApi.Models;

namespace TemplateDormApi.Repository;

public class CheckoutRepository : BaseRepository<CheckoutLog>
{
    public CheckoutRepository(AppDbContext context) : base(context) { }

    /// <summary>主键生成：MAX+1（表无序列，DDL 冻结不改；并发撞号由 DbSaveRetry 重试兜底）</summary>
    public async Task<long> NextLogIdAsync()
        => await _dbSet.AnyAsync() ? await _dbSet.MaxAsync(l => l.LogId) + 1L : 1L;

    /// <summary>某分配进行中的清算单（UK_D_CHECKOUT_ACTIVE 的代码侧快查，数据库约束为最终兜底）</summary>
    public Task<CheckoutLog?> GetActiveByAllocationAsync(long allocationId)
        => _dbSet.FirstOrDefaultAsync(l => l.AllocationId == allocationId && l.Status == "待清算");
}
