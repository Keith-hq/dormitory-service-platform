using Microsoft.EntityFrameworkCore;
using TemplateDormApi.Data;
using TemplateDormApi.Models;

namespace TemplateDormApi.Repository;

public class AdminRepository
{
    private readonly AppDbContext _context;

    public AdminRepository(AppDbContext context)
    {
        _context = context;
    }

    // 获取指定管理员的 TokenVersion（若不存在返回 0）
    public async Task<int> GetTokenVersionAsync(string adminId)
    {
        var admin = await _context.Admins
            .AsNoTracking()
            .Where(a => a.AdminId == adminId)
            .Select(a => a.TokenVersion)
            .FirstOrDefaultAsync();
        return admin; // 默认 0
    }

    // 自增 TokenVersion（用于停用管理员）
    public async Task<int> IncrementTokenVersionAsync(string adminId)
    {
        var admin = await _context.Admins.FindAsync(adminId);
        if (admin != null)
        {
            admin.TokenVersion++;
            await _context.SaveChangesAsync();
            return admin.TokenVersion;
        }
        return 0;
    }
}