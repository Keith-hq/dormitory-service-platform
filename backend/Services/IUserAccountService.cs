namespace TemplateDormApi.Services;

public interface IUserAccountService
{
    Task UpdateIsFirstLoginAsync(int accountId, string isFirstLogin);
}