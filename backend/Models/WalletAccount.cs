using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TemplateDormApi.Models;

/// <summary>
/// 钱包账户实体（映射 D_Wallet_Account）。
/// 表名/列名全大写：Oracle 提供商会按映射原样加引号生成 SQL，
/// 混合大小写与建表实际存储不一致（ORA-00942/00904，2026-08-15 实测修正）。
/// </summary>
[Table("D_WALLET_ACCOUNT", Schema = "DORM_OPER")]
public class WalletAccount
{
    [Key]
    [Column("STUDENT_ID")]
    public string StudentId { get; set; } = string.Empty;

    [Column("BALANCE")]
    public decimal Balance { get; set; }
}
