using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TemplateDormApi.Models;

/// <summary>
/// 钱包账户实体（映射 D_Wallet_Account）
/// </summary>
[Table("D_Wallet_Account", Schema = "DORM_OPER")]
public class WalletAccount
{
    [Key]
    [Column("Student_ID")]
    public string StudentId { get; set; } = string.Empty;

    [Column("Balance")]
    public decimal Balance { get; set; }
}
