using Microsoft.AspNetCore.Http;
using TemplateDormApi.Exceptions;
using TemplateDormApi.Repository;

namespace TemplateDormApi.Services;

public interface IStudentIdentityService
{
    Task EnsureOwnStudentIdAsync(int accountId, string studentId, CancellationToken cancellationToken);
}

public sealed class StudentIdentityService : IStudentIdentityService
{
    private readonly UserAccountRepository _userAccountRepository;

    public StudentIdentityService(UserAccountRepository userAccountRepository)
    {
        _userAccountRepository = userAccountRepository;
    }

    public async Task EnsureOwnStudentIdAsync(
        int accountId,
        string studentId,
        CancellationToken cancellationToken)
    {
        var currentStudentId = await _userAccountRepository.GetStudentIdByAccountIdAsync(
            accountId,
            cancellationToken);

        if (currentStudentId is null ||
            !string.Equals(currentStudentId, studentId, StringComparison.Ordinal))
        {
            throw new BusinessException(
                StatusCodes.Status403Forbidden,
                "无权访问其他学生的数据",
                StatusCodes.Status403Forbidden);
        }
    }
}
