using Microsoft.AspNetCore.Http;
using TemplateDormApi.Data;
using TemplateDormApi.Exceptions;

namespace TemplateDormApi.Repository;

/// <summary>
/// 当前阶段的业务仓储骨架。保留 DbContext 注入和方法边界，但在数据库设计获批前不执行写入。
/// </summary>
public abstract class FrameworkRepositoryBase
{
    protected FrameworkRepositoryBase(AppDbContext context)
    {
        DbContext = context;
    }

    protected AppDbContext DbContext { get; }

    protected static Task<T> PendingAsync<T>(
        string operationId,
        string reason,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromException<T>(new BusinessException(
            StatusCodes.Status501NotImplemented,
            $"{operationId} 接口框架已就绪，数据库实现待确认：{reason}",
            StatusCodes.Status501NotImplemented));
    }
}
