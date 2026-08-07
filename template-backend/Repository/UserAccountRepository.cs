using Microsoft.EntityFrameworkCore;
using TemplateDormApi.Data;

namespace TemplateDormApi.Repository;

/// <summary>
/// 用户账户查询仓储。
/// </summary>
public class UserAccountRepository
{
    private readonly AppDbContext _context;

    public UserAccountRepository(AppDbContext context)
    {
        _context = context;
    }

    public Task<int?> GetByStudentIdAsync(string studentId)
    {
        return _context.UserAccounts
            .AsNoTracking()
            .Where(account => account.StudentId == studentId)
            .Select(account => (int?)account.AccountId)
            .SingleOrDefaultAsync();
    }

    public Task<int?> GetByAdminIdAsync(string adminId)
    {
        return _context.UserAccounts
            .AsNoTracking()
            .Where(account => account.AdminId == adminId)
            .Select(account => (int?)account.AccountId)
            .SingleOrDefaultAsync();
    }
}
