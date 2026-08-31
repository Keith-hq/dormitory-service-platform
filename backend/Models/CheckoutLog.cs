namespace TemplateDormApi.Models;

/// <summary>
/// 退宿清算记录（映射 D_Checkout_Log）
/// 状态机：待清算 →（settle 两步校验失败）已拒绝 /（confirm）已通过 /（cancel）已取消。
/// UK_D_CHECKOUT_ACTIVE：同一分配最多一条「待清算」记录，防重复提交。
/// 主键由序列 SEQ_D_CHECKOUT_LOG_ID + 触发器生成（迁移 023），EF 侧配置 ValueGeneratedOnAdd。
/// </summary>
public class CheckoutLog
{
    /// <summary>清算记录ID（LOG_ID）</summary>
    public int LogId { get; set; }

    /// <summary>住宿分配ID（ALLOCATION_ID，FK → D_Bed_Allocation；与 BedAllocation.AllocationId 同为 long，保证 Find 主键类型一致）</summary>
    public long AllocationId { get; set; }

    /// <summary>登记时间（REQUEST_TIME，DDL DEFAULT SYSDATE，代码侧显式赋值）</summary>
    public DateTime RequestTime { get; set; }

    /// <summary>终态时间（RESULT_TIME）：拒绝/确认/取消时写入</summary>
    public DateTime? ResultTime { get; set; }

    /// <summary>水电费校验结果（FEE_CHECK）：通过 / 未通过；NULL=未执行</summary>
    public string? FeeCheck { get; set; }

    /// <summary>物品校验结果（ITEM_CHECK，共享物品）：通过 / 未通过；NULL=未执行</summary>
    public string? ItemCheck { get; set; }

    /// <summary>状态（STATUS）：待清算 / 已通过 / 已拒绝 / 已取消</summary>
    public string Status { get; set; } = "待清算";

    /// <summary>拒绝原因（REJECT_REASON）：两步校验未通过项逐项列出</summary>
    public string? RejectReason { get; set; }
}
