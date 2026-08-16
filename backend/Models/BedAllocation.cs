namespace TemplateDormApi.Models;

/// <summary>
/// 学生床位分配记录，对应 D_BED_ALLOCATION。
/// 刘润东为床位分配唯一数据拥有者：入住/调寝/退宿/清算全流程读写均在本模块（住宿查询模块只读）。
/// 主键由序列 SEQ_D_BED_ALLOCATION_ID + 触发器生成（迁移 023，WHEN NEW IS NULL 兼容显式值），
/// EF 侧配置 ValueGeneratedOnAdd（AppDbContext），应用层不再 MAX+1。
/// 并发保证：UK_D_BED_ALLOC_ACTIVE（房间+在住床位唯一）；CheckOut_Date 作并发令牌（AppDbContext 中配置），
/// 同一分配只能被一个事务写入退宿日期（调寝并发"仅一次生效"、退宿幂等均依赖此）。
/// </summary>
public sealed class BedAllocation
{
    public long AllocationId { get; set; }
    public string? StudentId { get; set; }
    public int? RoomId { get; set; }
    public int BedNo { get; set; }
    public DateTime CheckInDate { get; set; }
    public DateTime? CheckOutDate { get; set; }
}
