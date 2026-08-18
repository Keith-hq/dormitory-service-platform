using TemplateDormApi.DTO;
using TemplateDormApi.Models;

namespace TemplateDormApi.Services;

public interface IAllocationService
{
    /// <summary>
    /// DORM-08 入住分配（并发唯一）：校验学生/房间/床位后新增分配并占用+1。
    /// currentAccountId 用于并发用例（IT-C2-002）studentId 缺失时从登录态解析；
    /// 归属校验：学生自助时 body studentId 必须与登录态一致（非本人 403），宿管（isDormAdmin）可代办。
    /// </summary>
    Task<BedAllocation> CreateAsync(AllocationCreateDto dto, int? currentAccountId = null, bool isDormAdmin = false);

    /// <summary>
    /// DORM-09 调寝（旧房-1 新房+1 同一事务）：原分配写入退宿日期、新分配指向目标床位。
    /// 并发调寝同一学生仅一次生效（CheckOut_Date 并发令牌 + 重读判定）。
    /// </summary>
    Task<BedAllocation> TransferAsync(int allocationId, AllocationTransferDto dto);

    /// <summary>DORM-10 房间住户：当前在住学生信息（联学生姓名）</summary>
    Task<object> GetOccupantsAsync(int roomId);
}
