using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using TemplateDormApi.Data;
using TemplateDormApi.DTO;
using TemplateDormApi.Exceptions;
using TemplateDormApi.Models;

namespace TemplateDormApi.Repository;

/// <summary>
/// 门岗登记仓储（VST-01/02/03）。
/// 访客到访登记 → 扫码核验（校验 D_Visitor_Authorization.Authorization_Token）→ 离开记录。
/// </summary>
public sealed class VisitorRegistryRepository : FrameworkRepositoryBase
{
    public VisitorRegistryRepository(AppDbContext context) : base(context) { }

    /// <summary>VST-01 门岗登记访客到访（状态=待核验）。</summary>
    public async Task<VisitorRegistry> CreateAsync(
        CreateVisitorRegistryRequest request,
        CancellationToken cancellationToken)
    {
        // 规范化学号：空串 → null（不关联外键）；非空须存在于 D_Student，否则给出友好提示而非 FK 500
        var studentId = string.IsNullOrWhiteSpace(request.StudentId) ? null : request.StudentId.Trim();
        if (studentId is not null)
        {
            var exists = await DbContext.Set<Student>()
                .CountAsync(s => s.StudentId == studentId, cancellationToken) > 0;
            if (!exists)
            {
                throw new BusinessException(400, "被访学生不存在，请核对学号");
            }
        }

        var registry = new VisitorRegistry
        {
            VisitorName = request.VisitorName,
            Phone = request.Phone,
            StudentId = studentId,
            EnterTime = DateTime.Now,
            Status = "待核验"
        };
        DbContext.Set<VisitorRegistry>().Add(registry);
        await DbContext.SaveChangesAsync(cancellationToken);
        return registry;
    }

    /// <summary>VST-02 扫码核验：校验通行码后关联到待核验登记并置为已核验。</summary>
    public async Task<VisitorRegistry> VerifyAsync(
        long registryId,
        string qrToken,
        CancellationToken cancellationToken)
    {
        var registry = await DbContext.Set<VisitorRegistry>()
            .FirstOrDefaultAsync(r => r.RegistryId == registryId, cancellationToken)
            ?? throw new BusinessException(404, "登记记录不存在", StatusCodes.Status404NotFound);

        if (registry.Status != "待核验")
        {
            throw new BusinessException(400, "该登记已核验或已离开，不能重复操作");
        }

        // 校验通行码：复用 D_Visitor_Authorization 的 Authorization_Token
        var auth = await DbContext.Set<VisitorAuthorization>()
            .FirstOrDefaultAsync(a => a.AuthorizationToken == qrToken, cancellationToken)
            ?? throw new BusinessException(404, "通行码无效", StatusCodes.Status404NotFound);

        if (auth.Status != "有效")
        {
            throw new BusinessException(400, "通行码已失效或已撤销");
        }
        if (auth.ExpiresTime < DateTime.Now)
        {
            throw new BusinessException(400, "通行码已过期");
        }
        if (!string.Equals(auth.VisitorName, registry.VisitorName, StringComparison.Ordinal))
        {
            throw new BusinessException(400, "通行码与登记访客姓名不一致");
        }

        registry.Status = "已核验";
        registry.QrToken = qrToken;
        await DbContext.SaveChangesAsync(cancellationToken);
        return registry;
    }

    /// <summary>在场(尚未离场)访客登记列表，供门岗逐个办理离场。</summary>
    public Task<List<VisitorRegistry>> GetActiveAsync(CancellationToken cancellationToken)
        => DbContext.Set<VisitorRegistry>()
            .AsNoTracking()
            .Where(r => r.Status != "已离开")
            .OrderByDescending(r => r.EnterTime)
            .Take(200)
            .ToListAsync(cancellationToken);

    /// <summary>VST-03 记录访客离开。</summary>
    public async Task<VisitorRegistry> RecordExitAsync(
        long registryId,
        CancellationToken cancellationToken)
    {
        var registry = await DbContext.Set<VisitorRegistry>()
            .FirstOrDefaultAsync(r => r.RegistryId == registryId, cancellationToken)
            ?? throw new BusinessException(404, "登记记录不存在", StatusCodes.Status404NotFound);

        if (registry.Status == "已离开")
        {
            throw new BusinessException(400, "该访客已离开");
        }

        registry.Status = "已离开";
        registry.ExitTime = DateTime.Now;

        // 闭环：访客确认离开后，把本次核验所用的学生授权码置为“已过期”，
        // 学生端对应访客预约随即失效(与“撤销/到期后通行码失效”语义一致)。
        if (!string.IsNullOrWhiteSpace(registry.QrToken))
        {
            var authorization = await DbContext.Set<VisitorAuthorization>()
                .FirstOrDefaultAsync(
                    a => a.AuthorizationToken == registry.QrToken && a.Status == "有效",
                    cancellationToken);
            if (authorization is not null)
            {
                authorization.Status = "已过期";
            }
        }

        await DbContext.SaveChangesAsync(cancellationToken);
        return registry;
    }
}
