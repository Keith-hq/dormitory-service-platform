using TemplateDormApi.Repository;

namespace TemplateDormApi.Services;

public class UserAccountService : IUserAccountService
{
    private readonly UserAccountRepository _userAccountRepository;

    public UserAccountService(UserAccountRepository userAccountRepository)
    {
        _userAccountRepository = userAccountRepository;
    }

    public async Task UpdateIsFirstLoginAsync(int accountId, string isFirstLogin)
    {
        await _userAccountRepository.UpdateIsFirstLoginAsync(accountId, isFirstLogin);
    }
}