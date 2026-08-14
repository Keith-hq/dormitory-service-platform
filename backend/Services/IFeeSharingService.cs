using TemplateDormApi.DTO;
using TemplateDormApi.Models;

namespace TemplateDormApi.Services;

/// <summary>
/// 水电分摊服务接口
/// </summary>
public interface IFeeSharingService
{
    /// <summary>每月1日调用：批量生成当月全部分摊</summary>
    Task CalcMonthlyFee(string yearMonth);

    /// <summary>
    /// 退宿时调用：为退宿学生结算当月分摊。
    /// 一审 R1：本方法无事务（SP 内部不 COMMIT），事务由调用方统一管理——
    /// 独立入口（Controller/定时任务）需在外层开启并提交事务；
    /// 退宿流程（刘润东）可在其自身事务内直接调用本方法或 SP。
    /// </summary>
    Task CalcCheckoutFee(string studentId, int allocationId);

    /// <summary>查询某学生某月的个人分摊明细</summary>
    Task<List<FeeDetail>> GetFeeDetail(string studentId, string yearMonth);
}
