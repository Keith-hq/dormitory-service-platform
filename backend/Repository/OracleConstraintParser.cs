using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Oracle.ManagedDataAccess.Client;

namespace TemplateDormApi.Repository;

/// <summary>
/// Oracle 唯一约束冲突解析：从 DbUpdateException 提取约束名（如 UK_D_ROOM_BUILDING_NO），
/// 供调用方把 UK 冲突翻译为业务错误（409 / "已存在跳过"）而非 500。
/// 原 DbSaveRetry.TryGetUniqueConstraintName 逻辑迁移至此（DbSaveRetry 随迁移 023 序列化主键而废弃）。
/// </summary>
public static class OracleConstraintParser
{
    /// <summary>从 DbUpdateException 提取 Oracle 唯一约束名（如 UK_D_BED_ALLOC_ACTIVE）；非唯一冲突返回 null。</summary>
    public static string? TryGetUniqueConstraintName(DbUpdateException ex)
    {
        for (var inner = ex.InnerException; inner != null; inner = inner.InnerException)
        {
            // Oracle 提供方：唯一冲突为 OracleException{Number:1}。
            // 兼容消息前缀判断：InMemory 单测替身无法构造 OracleException，用同构消息模拟冲突。
            var isUniqueViolation = inner is OracleException { Number: 1 }
                || inner.Message.StartsWith("ORA-00001", StringComparison.Ordinal);
            if (!isUniqueViolation)
                continue;

            var match = Regex.Match(inner.Message, @"constraint\s*\([^.)]*\.([A-Za-z0-9_$#]+)\)");
            if (match.Success)
                return match.Groups[1].Value;
        }
        return null;
    }
}
