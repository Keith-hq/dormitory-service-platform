using TemplateDormApi.DTO;

namespace TemplateDormApi.Services;

/// <summary>
/// 钱包服务接口（STU-05 缴费 / STU-06 钱包查询 / STU-07 充值）。
/// 写操作走存储过程（SP 自 COMMIT，C# 不包事务），返回结果码。
/// </summary>
public interface IWalletService
{
    /// <summary>人工缴费（STU-05），返回 SP_Manual_Pay 结果码（0 成功/幂等重放）</summary>
    Task<int> ManualPay(int detailId, string studentId, string idempotencyKey);

    /// <summary>钱包充值（STU-07，无钱包行自动开户），返回 SP_Recharge 结果码（0 成功/幂等重放）</summary>
    Task<int> Recharge(string studentId, decimal amount, string idempotencyKey);

    /// <summary>查询学生钱包（STU-06）：余额 + 指定月份流水</summary>
    Task<WalletViewDto> GetWallet(string studentId, string yearMonth);

    /// <summary>
    /// 查询学生账单与分摊明细（STU-04）：yearMonth 缺省返回全部账期
    /// （含未结明细，供退宿欠费检查），指定时仅返回该账期。
    /// </summary>
    Task<StudentFeesDto> GetStudentFees(string studentId, string? yearMonth);
}
