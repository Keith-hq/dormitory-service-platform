using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TemplateDormApi.Models;

/// <summary>
/// 钱包流水实体（映射 D_Wallet_Log）
/// </summary>
[Table("D_Wallet_Log", Schema = "DORM_OPER")]
public class WalletLog
{
    [Key]
    [Column("Log_ID")]
    public int LogId { get; set; }

    [Column("Student_ID")]
    public string StudentId { get; set; } = string.Empty;

    [Column("Amount")]
    public decimal Amount { get; set; }

    [Column("Transaction_Type")]
    public string TransactionType { get; set; } = string.Empty;

    [Column("Before_Balance")]
    public decimal BeforeBalance { get; set; }

    [Column("After_Balance")]
    public decimal AfterBalance { get; set; }

    [Column("Detail_ID")]
    public int? DetailId { get; set; }

    [Column("Idempotency_Key")]
    public string IdempotencyKey { get; set; } = string.Empty;

    [Column("Create_Time")]
    public DateTime CreateTime { get; set; }
}
