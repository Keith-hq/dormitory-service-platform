using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using TemplateDormApi.Data;
using TemplateDormApi.Models;

namespace TemplateDormApi.Repository;

/// <summary>
/// 信用分数据访问，集中处理 Oracle 行锁与 InMemory 退化路径。
/// </summary>
public class CreditRepository
{
    private readonly AppDbContext _context;

    public CreditRepository(AppDbContext context)
    {
        _context = context;
    }

    private bool IsInMemory => string.Equals(
        _context.Database.ProviderName,
        "Microsoft.EntityFrameworkCore.InMemory",
        StringComparison.Ordinal);

    public Task<CreditLog?> FindLogByEventKeyAsync(string eventKey, CancellationToken cancellationToken)
    {
        return _context.CreditLogs
            .AsNoTracking()
            .SingleOrDefaultAsync(log => log.EventKey == eventKey, cancellationToken);
    }

    public Task<CreditAccount?> GetAccountAsync(string studentId, CancellationToken cancellationToken)
    {
        return _context.CreditAccounts
            .AsNoTracking()
            .SingleOrDefaultAsync(account => account.StudentId == studentId, cancellationToken);
    }

    public async Task<IDbContextTransaction?> BeginTransactionAsync(CancellationToken cancellationToken)
    {
        return IsInMemory
            ? null
            : await _context.Database.BeginTransactionAsync(cancellationToken);
    }

    public async Task LockStudentAsync(string studentId, CancellationToken cancellationToken)
    {
        if (IsInMemory)
        {
            return;
        }

        var query = _context.Students
            .FromSqlInterpolated($"SELECT * FROM D_STUDENT WHERE STUDENT_ID = {studentId} FOR UPDATE WAIT 5");

        await foreach (var _ in query.AsAsyncEnumerable().WithCancellation(cancellationToken))
        {
            return;
        }
    }

    public async Task<CreditAccount?> GetAccountForUpdateAsync(
        string studentId,
        CancellationToken cancellationToken)
    {
        if (IsInMemory)
        {
            return await _context.CreditAccounts
                .FirstOrDefaultAsync(account => account.StudentId == studentId, cancellationToken);
        }

        var query = _context.CreditAccounts
            .FromSqlInterpolated(
                $"SELECT * FROM D_CREDIT_ACCOUNT WHERE STUDENT_ID = {studentId} FOR UPDATE WAIT 5");

        await foreach (var account in query.AsAsyncEnumerable().WithCancellation(cancellationToken))
        {
            return account;
        }

        return null;
    }

    public void AddAccount(CreditAccount account)
    {
        _context.CreditAccounts.Add(account);
    }

    public void AddLog(CreditLog log)
    {
        _context.CreditLogs.Add(log);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        return _context.SaveChangesAsync(cancellationToken);
    }

    public Task<List<CreditLog>> GetRecentLogsAsync(
        string studentId,
        int count,
        CancellationToken cancellationToken)
    {
        return _context.CreditLogs
            .AsNoTracking()
            .Where(log => log.StudentId == studentId)
            .OrderByDescending(log => log.CreateTime)
            .ThenByDescending(log => log.LogId)
            .Take(count)
            .ToListAsync(cancellationToken);
    }

    public Task<List<string>> GetResetCandidateStudentIdsAsync(CancellationToken cancellationToken)
    {
        return _context.CreditAccounts
            .AsNoTracking()
            .Where(account => account.CurrentScore != 100)
            .Select(account => account.StudentId)
            .ToListAsync(cancellationToken);
    }

    public void ClearTracking()
    {
        _context.ChangeTracker.Clear();
    }
}
