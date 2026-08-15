using Microsoft.EntityFrameworkCore;
using TemplateDormApi.Data;
using TemplateDormApi.Models;

namespace TemplateDormApi.Repository;

public class CheckoutRepository : BaseRepository<CheckoutLog>
{
    public CheckoutRepository(AppDbContext context) : base(context) { }

    /// <summary>某分配进行中的清算单（UK_D_CHECKOUT_ACTIVE 的代码侧快查，数据库约束为最终兜底）</summary>
    public Task<CheckoutLog?> GetActiveByAllocationAsync(long allocationId)
        => _dbSet.FirstOrDefaultAsync(l => l.AllocationId == allocationId && l.Status == "待清算");
}
