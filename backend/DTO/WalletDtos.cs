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

/// <summary>STU-06 钱包视图：余额 + 指定月份流水 + 低余额提醒</summary>
public class WalletViewDto
{
    public string StudentId { get; set; } = string.Empty;
    public decimal Balance { get; set; }
    /// <summary>低余额阈值（服务端常量，契约未给具体数值，待契约确认后收口）</summary>
    public decimal LowBalanceThreshold { get; set; }
    /// <summary>余额低于阈值时为 true（契约"含低余额提醒"字段）</summary>
    public bool LowBalanceWarning { get; set; }
    public List<WalletLogDto> Logs { get; set; } = new();
}

/// <summary>STU-04 学生账单明细条目（GET /students/{studentId}/fees 的 items 元素）</summary>
public class StudentFeeItemDto
{
    public int DetailId { get; set; }
    public long FeeId { get; set; }
    /// <summary>账单账期（D_Utility_Fee.Year_Month，与 items 外层 yearMonth 同维度）</summary>
    public string YearMonth { get; set; } = string.Empty;
    public int RoomId { get; set; }
    public int StayDays { get; set; }
    public int TotalDays { get; set; }
    public decimal WaterShare { get; set; }
    public decimal PowerShare { get; set; }
    /// <summary>应缴合计 = WaterShare + PowerShare（沿用遗留 feesharing/detail 口径）</summary>
    public decimal Total { get; set; }
    public string BillType { get; set; } = string.Empty;
    public string IsPaid { get; set; } = "否";
}

/// <summary>
/// STU-04 学生账单查询结果：yearMonth 缺省返回全部账期（含未结明细，
/// 供 IT-C2-003 退宿欠费阻断检查），指定时仅返回该账期。
/// </summary>
public class StudentFeesDto
{
    public string StudentId { get; set; } = string.Empty;
    /// <summary>请求的账期回显；缺省查询时为 null（表示全部账期）</summary>
    public string? YearMonth { get; set; }
    public int Count { get; set; }
    public List<StudentFeeItemDto> Items { get; set; } = new();
}
