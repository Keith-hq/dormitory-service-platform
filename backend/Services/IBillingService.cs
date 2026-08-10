using TemplateDormApi.Models;

namespace TemplateDormApi.Services;

/// <summary>
/// 账单划扣与断电判定服务接口
/// </summary>
public interface IBillingService
{
    /// <summary>自动扣款：指定尝试次数和月份，遍历未缴账单扣款</summary>
    Task AutoDeduct(int attemptNo, string yearMonth);

    /// <summary>断电判定：第3次扣款后扫描，未缴电费的房间断电</summary>
    Task CheckPowerCut(string yearMonth);

    /// <summary>恢复供电：扫描断电房间，全部缴清则恢复</summary>
    Task RestorePower();

    /// <summary>查询钱包余额</summary>
    Task<decimal> GetBalance(string studentId);

    /// <summary>查询钱包流水</summary>
    Task<List<WalletLog>> GetWalletLogs(string studentId, string yearMonth);

    /// <summary>查询房间供电状态</summary>
    Task<string> GetPowerStatus(int roomId);
}
