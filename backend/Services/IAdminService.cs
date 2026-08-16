namespace TemplateDormApi.Services;

public interface IAdminService
{
    Task<int> IncrementTokenVersionAsync(string adminId);
}