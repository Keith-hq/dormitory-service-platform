namespace TemplateDormApi.Services;

/// <summary>
/// 公共设施预约服务接口
/// </summary>
public interface IFacilityBookingService
{
    /// <summary>预约设施：返回结果码（0=成功,1=设施不可用,2=信用分不足,3=已有活跃预约）</summary>
    Task<int> BookFacility(int facilityId, string studentId);

    /// <summary>开始使用</summary>
    Task<int> StartUse(int bookingId, string studentId);

    /// <summary>结束使用</summary>
    Task<int> EndUse(int bookingId, string studentId);

    /// <summary>过期巡检：15分钟未开始→失效</summary>
    Task ExpireBookings();

    /// <summary>超时自动完成：60分钟→已完成</summary>
    Task AutoComplete();
}
