using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TemplateDormApi.Models;

/// <summary>
/// 钱包流水实体（映射 D_Wallet_Log）。
/// 表名/列名全大写：Oracle 提供商会按映射原样加引号生成 SQL，
/// 混合大小写与建表实际存储不一致（ORA-00942/00904，2026-08-15 实测修正）。
/// </summary>
[Table("D_WALLET_LOG", Schema = "DORM_OPER")]
public class WalletLog
{
    [Key]
    [Column("LOG_ID")]
    public int LogId { get; set; }

    [Column("STUDENT_ID")]
    public string StudentId { get; set; } = string.Empty;

    [Column("AMOUNT")]
    public decimal Amount { get; set; }

    [Column("TRANSACTION_TYPE")]
    public string TransactionType { get; set; } = string.Empty;

    [Column("BEFORE_BALANCE")]
    public decimal BeforeBalance { get; set; }

    [Column("AFTER_BALANCE")]
    public decimal AfterBalance { get; set; }

    [Column("DETAIL_ID")]
    public int? DetailId { get; set; }

    [Column("IDEMPOTENCY_KEY")]
    public string IdempotencyKey { get; set; } = string.Empty;

    [Column("CREATE_TIME")]
    public DateTime CreateTime { get; set; }
}
