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

    public Task<int?> GetActiveByStudentIdAsync(string studentId, CancellationToken cancellationToken = default)
    {
        return _context.UserAccounts
            .AsNoTracking()
            .Where(account => account.StudentId == studentId && account.AccountStatus == "正常")
            .Select(account => (int?)account.AccountId)
            .SingleOrDefaultAsync(cancellationToken);
    }

    public Task<string?> GetStudentIdByAccountIdAsync(
        int accountId,
        CancellationToken cancellationToken = default)
    {
        return _context.UserAccounts
            .AsNoTracking()
            .Where(account => account.AccountId == accountId)
            .Select(account => account.StudentId)
            .SingleOrDefaultAsync(cancellationToken);
    }

    public Task<string?> GetAdminIdByAccountIdAsync(
        int accountId,
        CancellationToken cancellationToken = default)
    {
        return _context.UserAccounts
            .AsNoTracking()
            .Where(account => account.AccountId == accountId)
            .Select(account => account.AdminId)
            .SingleOrDefaultAsync(cancellationToken);
    }

    public Task<int?> GetByAdminIdAsync(string adminId)
    {
        return _context.UserAccounts
            .AsNoTracking()
            .Where(account => account.AdminId == adminId)
            .Select(account => (int?)account.AccountId)
            .SingleOrDefaultAsync();
    }

    public async Task UpdateIsFirstLoginAsync(int accountId, string isFirstLogin)
    {
        var user = await _context.UserAccounts.FindAsync(accountId);
        if (user != null)
        {
            user.IsFirstLogin = isFirstLogin;
            await _context.SaveChangesAsync();
        }
    }
}
