namespace TemplateDormApi.Models;

/// <summary>
/// 维修耗材消耗记录实体，对应 D_Repair_Material_Usage。
/// 写入必须走存储过程（SP_Consume_Material）：Usage_ID 由序列生成、
/// Idempotency_Key 由 SP 落库并受唯一索引 UK_D_REPAIR_MAT_USE_IDEM 兜底（迁移 019）。
/// </summary>
public class RepairMaterialUsage
{
    public int UsageId { get; set; }

    public int TicketId { get; set; }

    public int MaterialId { get; set; }

    public int Quantity { get; set; }

    public DateTime UseTime { get; set; }

    /// <summary>幂等键（迁移 019，VARCHAR2(100 CHAR)）；SP 层以"键非空"为前提做幂等去重。</summary>
    public string? IdempotencyKey { get; set; }
}
