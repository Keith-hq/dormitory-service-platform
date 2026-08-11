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

    /// <summary>退宿时调用：为退宿学生结算当月分摊</summary>
    Task CalcCheckoutFee(string studentId, int allocationId);

    /// <summary>查询某学生某月的个人分摊明细</summary>
    Task<List<FeeDetail>> GetFeeDetail(string studentId, string yearMonth);
}
