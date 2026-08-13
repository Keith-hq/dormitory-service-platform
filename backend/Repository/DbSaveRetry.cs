using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Oracle.ManagedDataAccess.Client;
using TemplateDormApi.Data;

namespace TemplateDormApi.Repository;

/// <summary>
/// 主键生成冲突重试辅助。
/// 背景：D_Leave_Application / D_Bed_Allocation / D_Checkout_Log / D_Audit_Event 主键无序列
/// （DDL 冻结不改），由应用层 MAX+1 生成；并发插入撞号时（ORA-00001 命中 PK 约束）
/// 重取 MAX 重试。业务唯一键冲突（如 UK_D_BED_ALLOC_ACTIVE）不在重试范围，由调用方翻译为业务错误。
/// 若团队后续同意新增"序列+触发器"迁移，可整体替换为 SEQ.NEXTVAL，调用点无需改动。
/// </summary>
public static class DbSaveRetry
{
    public const int MaxAttempts = 3;

    /// <summary>从 DbUpdateException 提取 Oracle 唯一约束名（如 UK_D_BED_ALLOC_ACTIVE）；非唯一冲突返回 null。</summary>
    public static string? TryGetUniqueConstraintName(DbUpdateException ex)
    {
        if (ex.InnerException is OracleException { Number: 1 } oracle)
        {
            var match = Regex.Match(oracle.Message, @"constraint\s*\([^.)]*\.([A-Za-z0-9_$#]+)\)");
            if (match.Success) return match.Groups[1].Value;
        }
        return null;
    }

    /// <summary>
    /// 带主键撞号重试的单实体插入：每次尝试用 nextId 重取主键，
    /// insert 委托内完成"取实体 → Add → SaveChanges"（SaveChanges 自身事务保证原子性）。
    /// PK 撞号 → ChangeTracker 清空后重来；业务唯一键冲突（UK_*）原样抛出由调用方翻译。
    /// </summary>
    public static async Task<T> InsertWithPkRetryAsync<T>(
        AppDbContext context,
        string pkConstraintName,
        Func<Task<long>> nextId,
        Func<long, Task<T>> insert)
    {
        for (var attempt = 1; ; attempt++)
        {
            var id = await nextId();
            try
            {
                return await insert(id);
            }
            catch (DbUpdateException ex) when (attempt < MaxAttempts)
            {
                var constraint = TryGetUniqueConstraintName(ex);
                if (constraint != null &&
                    constraint.Equals(pkConstraintName, StringComparison.OrdinalIgnoreCase))
                {
                    context.ChangeTracker.Clear(); // 丢弃撞号实体，重取 MAX 重试
                    continue;
                }
                throw;
            }
        }
    }
}
