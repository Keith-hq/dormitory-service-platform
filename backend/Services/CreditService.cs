using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore.Storage;
using Oracle.ManagedDataAccess.Client;
using TemplateDormApi.DTO;
using TemplateDormApi.Exceptions;
using TemplateDormApi.Models;
using TemplateDormApi.Repository;

namespace TemplateDormApi.Services;

/// <summary>
/// 信用分业务逻辑。
/// </summary>
public class CreditService : ICreditService
{
    private const int FrozenThreshold = 60;
    private const int InitialScore = 100;
    private const int RecentLogCount = 50;
    private readonly CreditRepository _creditRepository;
    private readonly UserAccountRepository _userAccountRepository;
    private readonly IFreezeNotifier _freezeNotifier;
    private readonly ILogger<CreditService> _logger;

    public CreditService(
        CreditRepository creditRepository,
        UserAccountRepository userAccountRepository,
        IFreezeNotifier freezeNotifier,
        ILogger<CreditService> logger)
    {
        _creditRepository = creditRepository;
        _userAccountRepository = userAccountRepository;
        _freezeNotifier = freezeNotifier;
        _logger = logger;
    }

    public async Task<CreditResultDto> DeductAsync(
        CreditDeductDto dto,
        CancellationToken cancellationToken)
    {
        ValidateDeduct(dto);
        await EnsureActiveStudentAsync(dto.StudentId, cancellationToken);

        var existingLog = await _creditRepository.FindLogByEventKeyAsync(
            dto.EventKey,
            cancellationToken);
        if (existingLog is not null)
        {
            return await ResolveIdempotentResultAsync(dto, existingLog, cancellationToken);
        }

        IDbContextTransaction? transaction = null;
        var shouldNotify = false;
        CreditResultDto result;

        try
        {
            transaction = await _creditRepository.BeginTransactionAsync(cancellationToken);
            _creditRepository.ClearTracking();
            await _creditRepository.LockStudentAsync(dto.StudentId, cancellationToken);
            var account = await _creditRepository.GetAccountForUpdateAsync(
                dto.StudentId,
                cancellationToken);

            if (account is null)
            {
                account = CreateAccount(dto.StudentId);
                _creditRepository.AddAccount(account);
            }

            var oldScore = account.CurrentScore;
            var newScore = oldScore + dto.ScoreChange;
            // 按次罚分封底：FloorAtZero 时在锁内按当前分数封底到 0，
            // 低分学生（如 1 分扣 2 分）不再因"结果低于 0"被拒，消除"已归还但扣分失败"窗口；
            // 流水仍记录请求的名义分值（ScoreChange），幂等内容比对保持一致。
            // 其他扣款场景（如账单扣款）不传 FloorAtZero，结果低于 0 仍拒绝（语义不回退）。
            if (newScore < 0 && dto.FloorAtZero)
            {
                newScore = 0;
            }

            if (newScore is < 0 or > InitialScore)
            {
                throw new BusinessException(400, "信用分变更后超出 0 到 100 范围");
            }

            account.CurrentScore = newScore;
            account.UpdatedTime = DateTime.Now;
            _creditRepository.AddLog(new CreditLog
            {
                StudentId = dto.StudentId,
                ScoreChange = dto.ScoreChange,
                Reason = dto.Reason,
                EventKey = dto.EventKey
            });

            await _creditRepository.SaveChangesAsync(cancellationToken);
            if (transaction is not null)
            {
                await transaction.CommitAsync(cancellationToken);
            }

            shouldNotify = oldScore >= FrozenThreshold && newScore < FrozenThreshold;
            result = ToResult(account);
        }
        catch (Exception exception) when (HasOracleNumber(exception, 1))
        {
            await RollbackAsync(transaction, CancellationToken.None);
            _creditRepository.ClearTracking();
            var conflictLog = await _creditRepository.FindLogByEventKeyAsync(
                dto.EventKey,
                cancellationToken);
            if (conflictLog is null)
            {
                throw new BusinessException(400, "数据冲突，请重试");
            }

            result = await ResolveIdempotentResultAsync(dto, conflictLog, cancellationToken);
        }
        catch (Exception exception) when (
            HasOracleNumber(exception, 54) || HasOracleNumber(exception, 30006))
        {
            await RollbackAsync(transaction, CancellationToken.None);
            _creditRepository.ClearTracking();
            throw new BusinessException(400, "系统繁忙，请重试");
        }
        catch
        {
            await RollbackAsync(transaction, CancellationToken.None);
            _creditRepository.ClearTracking();
            throw;
        }
        finally
        {
            if (transaction is not null)
            {
                await transaction.DisposeAsync();
            }
        }

        if (shouldNotify)
        {
            await TryNotifyFreezeAsync(dto.StudentId, cancellationToken);
        }

        return result;
    }

    public async Task<CreditResultDto> RestoreAsync(
        string studentId,
        int restoreScore,
        string eventKey,
        string reason,
        CancellationToken cancellationToken)
    {
        await EnsureActiveStudentAsync(studentId, cancellationToken);

        var existingLog = await _creditRepository.FindLogByEventKeyAsync(
            eventKey,
            cancellationToken);
        if (existingLog is not null)
        {
            var account = await GetOrCreateAccountAsync(studentId, cancellationToken);
            return ToResult(account);
        }

        IDbContextTransaction? transaction = null;
        CreditResultDto result;

        try
        {
            transaction = await _creditRepository.BeginTransactionAsync(cancellationToken);
            _creditRepository.ClearTracking();
            await _creditRepository.LockStudentAsync(studentId, cancellationToken);
            var account = await _creditRepository.GetAccountForUpdateAsync(
                studentId,
                cancellationToken);
            if (account is null)
            {
                throw new BusinessException(400, "信用账户不存在");
            }

            var newScore = account.CurrentScore + restoreScore;
            if (newScore is < 0 or > InitialScore)
            {
                throw new BusinessException(400, "信用分变更后超出 0 到 100 范围");
            }

            account.CurrentScore = newScore;
            account.UpdatedTime = DateTime.Now;
            _creditRepository.AddLog(new CreditLog
            {
                StudentId = studentId,
                ScoreChange = restoreScore,
                Reason = reason,
                EventKey = eventKey
            });

            await _creditRepository.SaveChangesAsync(cancellationToken);
            if (transaction is not null)
            {
                await transaction.CommitAsync(cancellationToken);
            }

            result = ToResult(account);
        }
        catch (Exception exception) when (HasOracleNumber(exception, 1))
        {
            await RollbackAsync(transaction, CancellationToken.None);
            _creditRepository.ClearTracking();
            var conflictLog = await _creditRepository.FindLogByEventKeyAsync(
                eventKey,
                cancellationToken);
            if (conflictLog is null)
            {
                throw new BusinessException(400, "数据冲突，请重试");
            }

            var account = await GetOrCreateAccountAsync(studentId, cancellationToken);
            result = ToResult(account);
        }
        catch (Exception exception) when (
            HasOracleNumber(exception, 54) || HasOracleNumber(exception, 30006))
        {
            await RollbackAsync(transaction, CancellationToken.None);
            _creditRepository.ClearTracking();
            throw new BusinessException(400, "系统繁忙，请重试");
        }
        catch
        {
            await RollbackAsync(transaction, CancellationToken.None);
            _creditRepository.ClearTracking();
            throw;
        }
        finally
        {
            if (transaction is not null)
            {
                await transaction.DisposeAsync();
            }
        }

        return result;
    }

    public async Task<CreditStatusDto> GetStatusAsync(
        string studentId,
        CancellationToken cancellationToken)
    {
        ValidateStudentId(studentId);
        await EnsureActiveStudentAsync(studentId, cancellationToken);
        var account = await GetOrCreateAccountAsync(studentId, cancellationToken);
        return ToStatus(account);
    }

    public async Task<CreditViewDto> GetViewAsync(
        string studentId,
        int accountId,
        CancellationToken cancellationToken)
    {
        ValidateStudentId(studentId);
        var currentStudentId = await _userAccountRepository.GetStudentIdByAccountIdAsync(
            accountId,
            cancellationToken);
        if (currentStudentId is null ||
            !string.Equals(currentStudentId, studentId, StringComparison.Ordinal))
        {
            throw new BusinessException(
                403,
                "无权查看他人信用分",
                StatusCodes.Status403Forbidden);
        }

        await EnsureActiveStudentAsync(studentId, cancellationToken);
        var account = await GetOrCreateAccountAsync(studentId, cancellationToken);
        var logs = await _creditRepository.GetRecentLogsAsync(
            studentId,
            RecentLogCount,
            cancellationToken);

        return new CreditViewDto
        {
            StudentId = studentId,
            CurrentScore = account.CurrentScore,
            IsFrozen = account.CurrentScore < FrozenThreshold,
            Items = logs.Select(log => new CreditLogItemDto
            {
                LogId = log.LogId,
                Reason = log.Reason,
                ScoreChange = log.ScoreChange,
                CreateTime = log.CreateTime
            }).ToList()
        };
    }

    public async Task<ResetResultDto> ResetMonthlyAsync(
        int year,
        int month,
        CancellationToken cancellationToken)
    {
        if (year is < 1 or > 9999 || month is < 1 or > 12)
        {
            throw new BusinessException(400, "重置年月无效");
        }

        var result = new ResetResultDto();
        var studentIds = await _creditRepository.GetResetCandidateStudentIdsAsync(cancellationToken);
        foreach (var studentId in studentIds)
        {
            cancellationToken.ThrowIfCancellationRequested();
            IDbContextTransaction? transaction = null;
            try
            {
                transaction = await _creditRepository.BeginTransactionAsync(cancellationToken);
                _creditRepository.ClearTracking();
                var account = await _creditRepository.GetAccountForUpdateAsync(studentId, cancellationToken);
                if (account is null || account.CurrentScore == InitialScore)
                {
                    result.Skipped++;
                    if (transaction is not null)
                    {
                        await transaction.CommitAsync(cancellationToken);
                    }
                    continue;
                }

                var eventKey = $"月度重置:{year:D4}-{month:D2}:{studentId}";
                var resetLog = await _creditRepository.FindLogByEventKeyAsync(eventKey, cancellationToken);
                if (resetLog is not null)
                {
                    result.Failed++;
                    await RollbackAsync(transaction, cancellationToken);
                    _creditRepository.ClearTracking();
                    continue;
                }

                // ScoreChange 必须基于锁内复核得到的旧值。
                var oldScore = account.CurrentScore;
                account.CurrentScore = InitialScore;
                account.UpdatedTime = DateTime.Now;
                _creditRepository.AddLog(new CreditLog
                {
                    StudentId = studentId,
                    ScoreChange = InitialScore - oldScore,
                    Reason = "月度重置",
                    EventKey = eventKey
                });
                await _creditRepository.SaveChangesAsync(cancellationToken);
                if (transaction is not null)
                {
                    await transaction.CommitAsync(cancellationToken);
                }
                result.Processed++;
            }
            catch (OperationCanceledException)
            {
                await RollbackAsync(transaction, CancellationToken.None);
                _creditRepository.ClearTracking();
                throw;
            }
            catch (Exception exception)
            {
                await RollbackAsync(transaction, CancellationToken.None);
                _creditRepository.ClearTracking();
                result.Failed++;
                _logger.LogWarning(exception, "学生 {StudentId} 月度信用分重置失败", studentId);
            }
            finally
            {
                if (transaction is not null)
                {
                    await transaction.DisposeAsync();
                }
            }
        }

        return result;
    }

    private async Task<CreditAccount> GetOrCreateAccountAsync(
        string studentId,
        CancellationToken cancellationToken)
    {
        var existing = await _creditRepository.GetAccountAsync(studentId, cancellationToken);
        if (existing is not null)
        {
            return existing;
        }

        IDbContextTransaction? transaction = null;
        try
        {
            transaction = await _creditRepository.BeginTransactionAsync(cancellationToken);
            _creditRepository.ClearTracking();
            await _creditRepository.LockStudentAsync(studentId, cancellationToken);
            var account = await _creditRepository.GetAccountForUpdateAsync(studentId, cancellationToken);
            if (account is null)
            {
                account = CreateAccount(studentId);
                _creditRepository.AddAccount(account);
                await _creditRepository.SaveChangesAsync(cancellationToken);
            }

            if (transaction is not null)
            {
                await transaction.CommitAsync(cancellationToken);
            }
            return account;
        }
        catch (Exception exception) when (
            HasOracleNumber(exception, 54) || HasOracleNumber(exception, 30006))
        {
            await RollbackAsync(transaction, CancellationToken.None);
            _creditRepository.ClearTracking();
            throw new BusinessException(400, "系统繁忙，请重试");
        }
        catch
        {
            await RollbackAsync(transaction, CancellationToken.None);
            _creditRepository.ClearTracking();
            throw;
        }
        finally
        {
            if (transaction is not null)
            {
                await transaction.DisposeAsync();
            }
        }
    }

    private async Task<CreditResultDto> ResolveIdempotentResultAsync(
        CreditDeductDto dto,
        CreditLog existingLog,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(existingLog.StudentId, dto.StudentId, StringComparison.Ordinal) ||
            existingLog.ScoreChange != dto.ScoreChange ||
            !string.Equals(existingLog.Reason, dto.Reason, StringComparison.Ordinal))
        {
            throw new BusinessException(400, "Event_Key 已存在且请求内容不一致");
        }

        var account = await _creditRepository.GetAccountAsync(dto.StudentId, cancellationToken);
        if (account is null)
        {
            throw new BusinessException(400, "数据冲突，请重试");
        }

        return ToResult(account);
    }

    private async Task EnsureActiveStudentAsync(string studentId, CancellationToken cancellationToken)
    {
        var accountId = await _userAccountRepository.GetActiveByStudentIdAsync(
            studentId,
            cancellationToken);
        if (!accountId.HasValue)
        {
            throw new BusinessException(40401, "学生账户不存在或已停用", StatusCodes.Status404NotFound);
        }
    }

    private async Task TryNotifyFreezeAsync(string studentId, CancellationToken cancellationToken)
    {
        try
        {
            await _freezeNotifier.NotifyAsync(new NotificationCreateDto
            {
                StudentId = studentId,
                Title = "信用分冻结提醒",
                Content = "您的信用分已低于 60 分，预约、借用和访客二维码功能已冻结；信用分将在每月 1 日恢复。",
                NotificationType = "信用"
            }, cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "学生 {StudentId} 的信用分冻结通知投递失败", studentId);
        }
    }

    private static CreditAccount CreateAccount(string studentId)
    {
        return new CreditAccount
        {
            StudentId = studentId,
            CurrentScore = InitialScore,
            UpdatedTime = DateTime.Now
        };
    }

    private static CreditStatusDto ToStatus(CreditAccount account)
    {
        return new CreditStatusDto
        {
            StudentId = account.StudentId,
            CurrentScore = account.CurrentScore,
            IsFrozen = account.CurrentScore < FrozenThreshold
        };
    }

    private static CreditResultDto ToResult(CreditAccount account)
    {
        return new CreditResultDto
        {
            StudentId = account.StudentId,
            CurrentScore = account.CurrentScore,
            IsFrozen = account.CurrentScore < FrozenThreshold
        };
    }

    private static void ValidateDeduct(CreditDeductDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);
        ValidateStudentId(dto.StudentId);
        if (dto.ScoreChange == 0 || dto.ScoreChange is < -100 or > 100)
        {
            throw new BusinessException(400, "scoreChange 必须在 -100 到 100 之间且不能为 0");
        }
        if (string.IsNullOrWhiteSpace(dto.Reason) || dto.Reason.Length > 200)
        {
            throw new BusinessException(400, "reason 不能为空且不能超过 200 个字符");
        }
        if (string.IsNullOrWhiteSpace(dto.EventKey) || dto.EventKey.Length > 100)
        {
            throw new BusinessException(400, "eventKey 不能为空且不能超过 100 个字符");
        }
    }

    private static void ValidateStudentId(string studentId)
    {
        if (string.IsNullOrWhiteSpace(studentId) || studentId.Length > 20)
        {
            throw new BusinessException(400, "studentId 不能为空且不能超过 20 个字符");
        }
    }

    private static async Task RollbackAsync(
        IDbContextTransaction? transaction,
        CancellationToken cancellationToken)
    {
        if (transaction is not null)
        {
            await transaction.RollbackAsync(cancellationToken);
        }
    }

    private static bool HasOracleNumber(Exception exception, int number)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is OracleException oracleException && oracleException.Number == number)
            {
                return true;
            }
        }

        return false;
    }
}
