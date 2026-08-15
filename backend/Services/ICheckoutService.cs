using TemplateDormApi.DTO;

namespace TemplateDormApi.Services;

/// <summary>
/// 退宿清算状态词表（D_Checkout_Log.STATUS 有 CHECK 约束，取值固定）。
/// 注：IT-C2-001 ④ 期望终态"已清算"为业务措辞，DB 侧 CHECK 约束只允许 Status='已通过'；
/// 查询响应层统一映射"已通过"→"已清算"（CheckoutService.BuildSummaryAsync），落库值不变，
/// 8/14 联调与前端确认取数口径后如有变更再调整。
/// </summary>
public static class CheckoutStatuses
{
    /// <summary>已登记，等待三步校验（DORM-11 创建）</summary>
    public const string Pending = "待清算";

    /// <summary>确认退宿完成（DORM-37，释放床位）</summary>
    public const string Confirmed = "已通过";

    /// <summary>三步校验未通过（DORM-36）</summary>
    public const string Rejected = "已拒绝";

    /// <summary>取消清算（DORM-38）</summary>
    public const string Cancelled = "已取消";
}

public interface ICheckoutService
{
    /// <summary>DORM-11 退宿登记：创建「待清算」记录（同一分配唯一，UK_D_CHECKOUT_ACTIVE）</summary>
    Task<object> RegisterAsync(long allocationId, CheckoutRegisterDto dto);

    /// <summary>DORM-35 清算状态查询（含床位/房间快照）</summary>
    Task<object> GetAsync(int checkoutId);

    /// <summary>
    /// DORM-36 开始清算：三步校验（水电费缴清/快递取走/共享物品归还）。
    /// 任一未通过 → 已拒绝并逐项记录原因；全部通过 → 写 CheckOut_Date 再调 calc SP（同一事务）。
    /// </summary>
    Task<object> SettleAsync(int checkoutId);

    /// <summary>DORM-37 确认退宿：释放床位（房间占用-1），幂等（重复调用不报错、不重复释放）</summary>
    Task<object> ConfirmAsync(int checkoutId, CheckoutConfirmDto dto);

    /// <summary>DORM-38 取消清算：回滚 settle 写入的退宿日期，床位恢复在住；审计留痕待审计公共服务接入后记录（IT-C2-005 ③）</summary>
    Task<object> CancelAsync(int checkoutId);
}
