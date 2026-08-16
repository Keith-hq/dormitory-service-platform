using TemplateDormApi.Repository;

namespace TemplateDormApi.Services;

public class AdminService : IAdminService
{
    private readonly AdminRepository _adminRepository;

    public AdminService(AdminRepository adminRepository)
    {
        _adminRepository = adminRepository;
    }

    public async Task<int> IncrementTokenVersionAsync(string adminId)
    {
        return await _adminRepository.IncrementTokenVersionAsync(adminId);
    }
}