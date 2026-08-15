using System.ComponentModel.DataAnnotations;

namespace TemplateDormApi.DTO;

/// <summary>STU-05 人工缴费请求体</summary>
public class ManualPayRequest
{
    /// <summary>分摊明细 ID（DORM-23 返回的 detailId）</summary>
    [Range(1, int.MaxValue, ErrorMessage = "明细ID必须大于0")]
    public int DetailId { get; set; }
}

/// <summary>STU-07 钱包充值请求体</summary>
public class RechargeRequest
{
    /// <summary>充值金额（上限对齐 D_Wallet_Account.Balance NUMBER(8,2)）</summary>
    [Range(typeof(decimal), "0.01", "999999.99", ErrorMessage = "充值金额必须在 0.01~999999.99 之间")]
    public decimal Amount { get; set; }
}

/// <summary>钱包流水条目（STU-06 返回的 logs 元素）</summary>
public class WalletLogDto
{
    public int LogId { get; set; }
    public decimal Amount { get; set; }
    public string TransactionType { get; set; } = string.Empty;
    public decimal BeforeBalance { get; set; }
    public decimal AfterBalance { get; set; }
    public int? DetailId { get; set; }
    public DateTime CreateTime { get; set; }
}

/// <summary>STU-06 钱包视图：余额 + 指定月份流水</summary>
public class WalletViewDto
{
    public string StudentId { get; set; } = string.Empty;
    public decimal Balance { get; set; }
    public List<WalletLogDto> Logs { get; set; } = new();
}
